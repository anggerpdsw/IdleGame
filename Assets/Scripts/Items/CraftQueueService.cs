using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items.Decomposition;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Manages the queue of craft jobs with unique IDs, persistence, and offline progress.
    /// Supports batch crafting, multiple concurrent jobs, and priority queue.
    /// </summary>
    public sealed class CraftQueueService
    {
        private readonly CraftRecipeRepository _repository;
        private readonly Dictionary<string, CraftJob> _jobs = new(); // JobId -> CraftJob
        private readonly Queue<string> _jobQueue = new();           // Order of jobs (for priority)
        private int _maxConcurrentJobs = 1;                          // Can be upgraded
        private int _activeJobCount = 0;

        // Events
        public event Action<string> OnJobStarted;       // jobId
        public event Action<string, float> OnJobProgress; // jobId, progress (0-1)
        public event Action<string> OnJobCompleted;     // jobId (results rolled by CraftService)
        public event Action<string> OnJobCancelled;     // jobId

        // ============ Properties ============
        public IReadOnlyDictionary<string, CraftJob> Jobs => _jobs;
        public IReadOnlyList<CraftJob> ActiveJobs => _jobs.Values.Where(j => j.IsActive).ToList();
        public IReadOnlyList<CraftJob> PendingJobs => _jobs.Values.Where(j => j.IsPending).OrderBy(j => j.StartTimeUtc).ToList();
        public IReadOnlyList<CraftJob> CompletedJobs => _jobs.Values.Where(j => j.IsComplete).ToList();
        public int MaxConcurrentJobs => _maxConcurrentJobs;
        public int ActiveJobCount => _activeJobCount;

        public CraftQueueService(CraftRecipeRepository repository)
        {
            _repository = repository;
        }

        // ============ Public API ============
        public string StartCraft(string recipeId, int count = 1)
        {
            if (!_repository.TryGetRecipe(recipeId, out var recipe))
            {
                Debug.LogWarning($"[CraftQueue] Recipe not found: {recipeId}");
                return null;
            }

            // Calculate total duration
            long baseTicks = (long)(recipe.BaseCraftTime * TimeSpan.TicksPerSecond);
            long additionalTicks = (long)(recipe.TimePerAdditionalUnit * TimeSpan.TicksPerSecond * (count - 1));
            long totalDurationTicks = baseTicks + additionalTicks;

            // Create job
            var job = CraftJob.Create(recipeId, count, totalDurationTicks);

            // Capture ingredient snapshot before transaction starts. Deep-copies recipe state
            // so refund/audit paths remain immune to recipe or database mutations post-creation.
            job.IngredientsSnapshot = recipe.Ingredients != null
                ? Array.ConvertAll(recipe.Ingredients, ing => CraftIngredientSnapshot.From(ing, count))
                : null;

            // P0-B step 10: build CraftExecutionSnapshot at job creation (I-21 — seed-at-build)
            var recipeSnapshot = new RecipeSnapshot(
                recipe.RecipeId, recipe.RecipeVersion, recipe.EquipmentType.ToString(), recipe.Rarity,
                job.IngredientsSnapshot != null
                    ? Array.ConvertAll(job.IngredientsSnapshot, s => new CraftIngredientSnapshot { ItemId = s.ItemId, Count = s.Count })
                    : Array.Empty<CraftIngredientSnapshot>());
            var materialsCosts = job.IngredientsSnapshot != null
                ? Array.ConvertAll(job.IngredientsSnapshot, s => new IngredientCost { ItemId = s.ItemId, Count = s.Count })
                : Array.Empty<IngredientCost>();
            var decomposedReqs = DecomposedRequirementResolver.Compute(recipe.Rarity);
            var decomposedCosts = DecomposedRequirementAggregator.SumPerJob(decomposedReqs, count);
            var progressionCosts = new IngredientCost[decomposedCosts.Count];
            for (int pi = 0; pi < decomposedCosts.Count; pi++)
                progressionCosts[pi] = new IngredientCost { ItemId = decomposedCosts[pi].ItemId, Count = decomposedCosts[pi].Count };
            var additionalCosts = recipe.AdditionalCosts != null
                ? Array.ConvertAll(recipe.AdditionalCosts, c => new CostEntry { CurrencyId = c.Currency.ToString(), Amount = c.Amount * count })
                : Array.Empty<CostEntry>();
            var currencySnap = new CurrencySnapshot(recipe.GoldCost * count, recipe.GemCost * count, additionalCosts);
            var costSnap = new CostSnapshot(materialsCosts, Array.Empty<IngredientCost>(), progressionCosts, currencySnap);
            var contextSnap = new CraftContextSnapshot(); // P0-D will populate via CraftContextBuilder
            var seed = (long)_rng.Next(1, int.MaxValue); // sentinel 0 banned per I-21 (lower bound 1 guarantees positive)
            job.ExecutionSnapshot = new CraftExecutionSnapshot(recipeSnapshot, costSnap, contextSnap, seed, count);
            job.CompletionSeed = seed;

            _jobs[job.JobId] = job;
            _jobQueue.Enqueue(job.JobId);

            // Try to start immediately if slot available
            TryStartNextJob();

            Debug.Log($"[CraftQueue] Started craft job {job.JobId} for recipe {recipeId} x{count} (duration: {TimeSpan.FromTicks(totalDurationTicks)})");
            return job.JobId;
        }

        public IReadOnlyList<string> StartBatchCraft(string recipeId, int count)
        {
            var jobIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var jobId = StartCraft(recipeId, 1);
                if (jobId != null) jobIds.Add(jobId);
            }
            return jobIds;
        }

        public void Update()
        {
            // Update active jobs progress
            foreach (var job in _jobs.Values.Where(j => j.IsActive))
            {
                float progress = job.Progress;
                OnJobProgress?.Invoke(job.JobId, progress);

                if (progress >= 1f)
                {
                    CompleteJob(job.JobId);
                }
            }

            // Try to start queued jobs
            while (_activeJobCount < _maxConcurrentJobs && _jobQueue.Count > 0)
            {
                TryStartNextJob();
            }
        }

        public CraftJob GetJob(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return job;
        }

        public IReadOnlyList<CraftJob> GetActiveJobs() => ActiveJobs;
        public IReadOnlyList<CraftJob> GetPendingJobs() => PendingJobs;
        public IReadOnlyList<CraftJob> GetCompletedJobs() => CompletedJobs;
        public IReadOnlyList<CraftJob> GetAllJobs() => _jobs.Values.ToList();

        public float GetJobProgress(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                return job.Progress;
            return 0f;
        }

        public TimeSpan GetJobTimeRemaining(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                return job.GetTimeRemaining();
            return TimeSpan.Zero;
        }

        public bool CancelJob(string jobId, RefundPolicy policy = RefundPolicy.ProgressBased)
        {
            if (!_jobs.TryGetValue(jobId, out var job)) return false;

            if (job.IsComplete) return false; // Can't cancel completed

            string reason = $"Cancelled (policy: {policy})";
            job.MarkCancelled(reason);
            _activeJobCount = Math.Max(0, _activeJobCount - 1);
            OnJobCancelled?.Invoke(jobId);

            // Refund handled by CraftService using policy
            return true;
        }

        public void ClearCompletedJobs()
        {
            var completed = _jobs.Values.Where(j => j.IsComplete).Select(j => j.JobId).ToList();
            foreach (var id in completed)
            {
                _jobs.Remove(id);
            }
        }

        public void SetMaxConcurrentJobs(int max)
        {
            _maxConcurrentJobs = Math.Max(1, max);
        }

        // ============ Offline Progress ============
        public void CalculateOfflineProgress()
        {
            var now = DateTime.UtcNow.Ticks;
            foreach (var job in _jobs.Values.Where(j => j.Status == CraftJobStatus.Crafting))
            {
                long elapsed = now - job.StartTimeUtc;
                if (elapsed >= job.DurationTicks)
                {
                    // Job would have completed while offline
                    CompleteJob(job.JobId);
                }
                // Progress events will fire on next Update()
            }
        }

        // ============ Persistence ============
        private static readonly System.Random _rng = new(); // static prevents millisecond-clock collision when multiple jobs start in the same tick
        public CraftQueueSaveData GetSaveData()
        {
            return new CraftQueueSaveData
            {
                Jobs = _jobs.Values.Select(j => new CraftJobSaveData
                {
                    JobId = j.JobId,
                    RecipeId = j.RecipeId,
                    StartTimeUtc = j.StartTimeUtc,
                    EndTimeUtc = j.EndTimeUtc,
                    DurationTicks = j.DurationTicks,
                    Count = j.Count,
                    CompletedCount = j.CompletedCount,
                    Status = j.Status,
                    Results = j.Results,
                    FailureReason = j.FailureReason,
                    IngredientsSnapshot = j.IngredientsSnapshot,
                    ExecutionSnapshot = j.ExecutionSnapshot,
                    CompletionSeed = j.CompletionSeed
                }).ToList(),
                MaxConcurrentJobs = _maxConcurrentJobs
            };
        }

        public void LoadFromSaveData(CraftQueueSaveData data)
        {
            _jobs.Clear();
            _jobQueue.Clear();
            _activeJobCount = 0;

            if (data == null || data.Jobs == null) return;

            foreach (var jobData in data.Jobs)
            {
                var job = new CraftJob
                {
                    JobId = jobData.JobId,
                    RecipeId = jobData.RecipeId,
                    StartTimeUtc = jobData.StartTimeUtc,
                    EndTimeUtc = jobData.EndTimeUtc,
                    DurationTicks = jobData.DurationTicks,
                    Count = jobData.Count,
                    CompletedCount = jobData.CompletedCount,
                    Status = jobData.Status,
                    Results = jobData.Results,
                    FailureReason = jobData.FailureReason,
                    IngredientsSnapshot = jobData.IngredientsSnapshot,
                    ExecutionSnapshot = jobData.ExecutionSnapshot,
                    CompletionSeed = jobData.CompletionSeed
                };
                _jobs[job.JobId] = job;

                // Re-queue active/pending jobs
                if (job.Status == CraftJobStatus.Crafting || job.Status == CraftJobStatus.Queued)
                {
                    _jobQueue.Enqueue(job.JobId);
                    if (job.Status == CraftJobStatus.Crafting)
                        _activeJobCount++;
                }
            }

            _maxConcurrentJobs = data.MaxConcurrentJobs > 0 ? data.MaxConcurrentJobs : 1;

            Debug.Log($"[CraftQueue] Loaded {_jobs.Count} jobs, {_activeJobCount} active, queue: {_jobQueue.Count}");
        }

        // ============ Private Methods ============
        private void TryStartNextJob()
        {
            if (_activeJobCount >= _maxConcurrentJobs) return;
            if (_jobQueue.Count == 0) return;

            string jobId = _jobQueue.Dequeue();
            if (_jobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == CraftJobStatus.Queued)
                {
                    job.MarkCrafting();
                    _activeJobCount++;
                    OnJobStarted?.Invoke(jobId);
                }
            }
        }

        private void CompleteJob(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job)) return;
            if (job.IsComplete) return; // Already completed

            _activeJobCount = Math.Max(0, _activeJobCount - 1);
            job.MarkComplete(null); // Results set by CraftService after roll
            OnJobCompleted?.Invoke(jobId);

            // Try to start next queued job
            TryStartNextJob();
        }
    }

    /// <summary>
    /// Refund policy for cancelled crafts.
    /// </summary>
    public enum RefundPolicy
    {
        None = 0,           // No refund
        Full = 1,           // Full refund always
        ProgressBased = 2,  // Refund based on progress (1 - progress)
        HalfAfterHalf = 3,  // Full refund before 50%, 50% refund after 50%, none after 90%
        Custom = 4          // Custom policy (configured per recipe)
    }

    /// <summary>
    /// Save data for CraftQueueService.
    /// </summary>
    [Serializable]
    public class CraftQueueSaveData
    {
        public List<CraftJobSaveData> Jobs = new();
        public int MaxConcurrentJobs = 1;
    }

}