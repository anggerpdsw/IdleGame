using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Manages the queue of craft jobs with unique IDs, persistence, and offline progress.
    /// State derived from EndTimeUtc: Queued (EndTimeUtc=0), Crafting (EndTimeUtc > UtcNow), ReadyToClaim (EndTimeUtc <= UtcNow).
    /// No mutable _activeJobCount counter; computed from jobs.
    /// </summary>
    public sealed class CraftQueueService
    {
        private readonly Dictionary<string, CraftJob> _jobs = new(); // JobId -> CraftJob
        private readonly Queue<string> _jobQueue = new();           // FIFO order for pending jobs
        private int _maxConcurrentJobs = 1;                          // Can be upgraded

        // Events
        public event Action<string> OnJobStarted;       // jobId (job began crafting)
        public event Action<string, float> OnJobProgress; // jobId, progress (0-1)
        public event Action<string> OnJobReadyToClaim;  // jobId (timer finished, ready for claim)
        public event Action<string, CraftJobStatus> OnJobStatusChanged; // jobId, new status (computed)

        // ============ Properties ============
        public IReadOnlyDictionary<string, CraftJob> Jobs => _jobs;
        public IReadOnlyList<CraftJob> ActiveJobs => _jobs.Values.Where(j => j.IsCrafting).ToList();
        public IReadOnlyList<CraftJob> PendingJobs => _jobs.Values.Where(j => j.IsQueued).OrderBy(j => j.EndTimeUtc).ToList();
        public IReadOnlyList<CraftJob> ReadyToClaimJobs => _jobs.Values.Where(j => j.IsReadyToClaim).ToList();
        public int MaxConcurrentJobs => _maxConcurrentJobs;
        public int ActiveJobCount => _jobs.Values.Count(j => j.IsCrafting);
        public bool HasAvailableSlot => ActiveJobCount < _maxConcurrentJobs;

        public CraftQueueService() { }

        // ============ Public API ============

        /// <summary>
        /// Enqueues a pre-built CraftJob. Job starts queued (EndTimeUtc=0).
        /// Caller must call TryStartNextJob or ensure slot available.
        /// </summary>
        public bool EnqueueJob(CraftJob job)
        {
            if (job == null) return false;
            if (string.IsNullOrEmpty(job.JobId)) return false;
            if (_jobs.ContainsKey(job.JobId)) return false;
            _jobs[job.JobId] = job;
            _jobQueue.Enqueue(job.JobId);
            return true;
        }

        public void Update()
        {
            // Fire progress for crafting jobs
            foreach (var job in _jobs.Values.Where(j => j.IsCrafting))
            {
                float progress = job.Progress;
                OnJobProgress?.Invoke(job.JobId, progress);
            }

            // Fire ReadyToClaim event once per job that just became ready
            foreach (var job in _jobs.Values.Where(j => j.IsReadyToClaim))
            {
                OnJobReadyToClaim?.Invoke(job.JobId);
            }

            // Try to start queued jobs while slots available
            while (HasAvailableSlot && _jobQueue.Count > 0)
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
        public IReadOnlyList<CraftJob> GetReadyToClaimJobs() => ReadyToClaimJobs;
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

        public void ClearCompletedJobs()
        {
            var readyToClaim = _jobs.Values.Where(j => j.IsReadyToClaim).Select(j => j.JobId).ToList();
            foreach (var id in readyToClaim)
            {
                _jobs.Remove(id);
            }
        }

        public bool RemoveJob(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job)) return false;
            if (!job.IsReadyToClaim) return false; // Only removable when ready to claim

            _jobs.Remove(jobId);
            return true;
        }

        public void SetMaxConcurrentJobs(int max)
        {
            _maxConcurrentJobs = Math.Max(1, max);
        }

        // ============ Persistence ============
        public CraftQueueSaveData GetSaveData()
        {
            return new CraftQueueSaveData
            {
                Jobs = _jobs.Values.Select(j => new CraftJobSaveData
                {
                    JobId = j.JobId,
                    RecipeId = j.RecipeId,
                    RecipeVersion = j.RecipeVersion,
                    EndTimeUtc = j.EndTimeUtc,
                    DurationTicks = j.DurationTicks,
                    Count = j.Count,
                    CompletionSeed = j.CompletionSeed
                }).ToList(),
                MaxConcurrentJobs = _maxConcurrentJobs
            };
        }

        public void LoadFromSaveData(CraftQueueSaveData data)
        {
            _jobs.Clear();
            _jobQueue.Clear();

            if (data == null || data.Jobs == null) return;

            foreach (var jobData in data.Jobs)
            {
                int recipeVersion = jobData.RecipeVersion > 0 ? jobData.RecipeVersion : 1;
                long completionSeed = jobData.CompletionSeed;

                var job = new CraftJob
                {
                    JobId = jobData.JobId,
                    RecipeId = jobData.RecipeId,
                    RecipeVersion = recipeVersion,
                    CompletionSeed = completionSeed,
                    EndTimeUtc = jobData.EndTimeUtc,
                    DurationTicks = jobData.DurationTicks,
                    Count = jobData.Count
                };
                _jobs[job.JobId] = job;

                // Re-queue pending jobs (EndTimeUtc == 0)
                if (job.IsQueued)
                {
                    _jobQueue.Enqueue(job.JobId);
                }
                // Active/ReadyToClaim jobs don't need queue entry
            }

            _maxConcurrentJobs = data.MaxConcurrentJobs > 0 ? data.MaxConcurrentJobs : 1;

            // Fire events for already-active/ready jobs so UI can reflect them immediately
            foreach (var job in _jobs.Values)
            {
                if (job.IsCrafting)
                {
                    OnJobStarted?.Invoke(job.JobId);
                }
                else if (job.IsReadyToClaim)
                {
                    OnJobReadyToClaim?.Invoke(job.JobId);
                }
            }

            Debug.Log($"[CraftQueue] Loaded {_jobs.Count} jobs, {ActiveJobCount} active, {ReadyToClaimJobs.Count} ready, queue: {_jobQueue.Count}");
        }

        // ============ Private Methods ============
        public void TryStartNextJob()
        {
            if (!HasAvailableSlot) return;
            if (_jobQueue.Count == 0) return;

            if (!_jobQueue.TryPeek(out string jobId)) return;

            if (_jobs.TryGetValue(jobId, out var job))
            {
                if (job.IsQueued)
                {
                    _jobQueue.Dequeue();
                    job.Start(); // Sets EndTimeUtc = now + DurationTicks

                    OnJobStatusChanged?.Invoke(jobId, job.Status);
                    OnJobStarted?.Invoke(jobId);
                }
                else
                {
                    // Job in queue is not queued, remove it to prevent blocking
                    _jobQueue.Dequeue();
                    Debug.LogWarning($"[CraftQueue] Removed invalid job {jobId} from queue (not queued).");
                }
            }
            else
            {
                _jobQueue.Dequeue();
                Debug.LogWarning($"[CraftQueue] Removed stale job ID {jobId} from queue.");
            }
        }
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

    /// <summary>
    /// Simplified save data for a single job.
    /// Only persists: JobId, RecipeId, RecipeVersion, CompletionSeed, EndTimeUtc, DurationTicks, Count.
    /// </summary>
    [Serializable]
    public class CraftJobSaveData
    {
        public string JobId;
        public string RecipeId;
        public int RecipeVersion = 1;
        public long EndTimeUtc;
        public long DurationTicks;
        public int Count = 1;
        public long CompletionSeed;
    }
}