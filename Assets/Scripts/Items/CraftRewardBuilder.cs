using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items.Random;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Fluent builder for creating CraftResultEntry objects.
    /// Eliminates repetitive entry creation code.
    /// </summary>
    public sealed class CraftRewardBuilder
    {
        private string _itemId;
        private int _count = 1;
        private int _quality = 0;
        private CraftRewardSource _source = CraftRewardSource.Normal;
        private bool _isCritical = false;
        private int _fixedLevel = 0;
        private int _fixedEnhance = 0;
        private int _socketCount = 0;

        private CraftRewardBuilder() { }

        public static CraftRewardBuilder Create() => new();

        public CraftRewardBuilder WithItemId(string itemId)
        {
            _itemId = itemId;
            return this;
        }

        public CraftRewardBuilder WithCount(int count)
        {
            _count = Mathf.Max(1, count);
            return this;
        }

        public CraftRewardBuilder WithQuality(int quality)
        {
            _quality = Mathf.Clamp(quality, 0, 5);
            return this;
        }

        public CraftRewardBuilder WithRandomQuality(int min, int max)
        {
            _quality = UnityEngine.Random.Range(min, max + 1);
            return this;
        }

        public CraftRewardBuilder WithSource(CraftRewardSource source)
        {
            _source = source;
            return this;
        }

        public CraftRewardBuilder AsCritical(bool isCritical = true)
        {
            _isCritical = isCritical;
            return this;
        }

        public CraftRewardBuilder WithFixedLevel(int level)
        {
            _fixedLevel = level;
            return this;
        }

        public CraftRewardBuilder WithFixedEnhance(int enhance)
        {
            _fixedEnhance = enhance;
            return this;
        }

        public CraftRewardBuilder WithSockets(int count)
        {
            _socketCount = Mathf.Max(0, count);
            return this;
        }

        public CraftResultEntry Build()
        {
            if (string.IsNullOrEmpty(_itemId))
                throw new InvalidOperationException("ItemId is required");

            return new CraftResultEntry
            {
                ItemId = _itemId,
                Count = _count,
                Quality = _quality,
                Source = _source.ToString(),
                IsCritical = _isCritical,
                FixedLevel = _fixedLevel,
                FixedEnhance = _fixedEnhance,
                SocketCount = _socketCount
            };
        }

        public List<CraftResultEntry> BuildMultiple(int count)
        {
            var entries = new List<CraftResultEntry>(count);
            for (int i = 0; i < count; i++)
            {
                entries.Add(Build());
            }
            return entries;
        }

        // ============ Convenience Static Methods ============

        public static CraftResultEntry Normal(string itemId, int count = 1, int quality = 0)
        {
            return Create().WithItemId(itemId).WithCount(count).WithQuality(quality).WithSource(CraftRewardSource.Normal).Build();
        }

        public static CraftResultEntry Critical(string itemId, int count = 1, int quality = 0, string variant = "Double")
        {
            return Create().WithItemId(itemId).WithCount(count).WithQuality(quality)
                .WithSource(CraftRewardSource.Critical).AsCritical(true).Build();
        }

        public static CraftResultEntry Guaranteed(string itemId, int count = 1, int quality = 0)
        {
            return Create().WithItemId(itemId).WithCount(count).WithQuality(quality).WithSource(CraftRewardSource.Guaranteed).Build();
        }

        public static CraftResultEntry Mastery(string itemId, int count = 1, int quality = 0)
        {
            return Create().WithItemId(itemId).WithCount(count).WithQuality(quality).WithSource(CraftRewardSource.Mastery).Build();
        }

        public static CraftResultEntry Event(string itemId, string eventId, int count = 1, int quality = 0)
        {
            return Create().WithItemId(itemId).WithCount(count).WithQuality(quality)
                .WithSource(CraftRewardSource.Event).Build();
        }

        public static CraftResultEntry FromRecipeResult(CraftResult recipeResult, CraftRewardSource source = CraftRewardSource.Normal, IRandomProvider rng = null)
        {
            var provider = rng ?? new UnityRandomProvider();
            int count = provider.Range(recipeResult.MinCount, recipeResult.MaxCount + 1);
            int quality = provider.Range(recipeResult.MinQuality, recipeResult.MaxQuality + 1);

            return Create()
                .WithItemId(recipeResult.ItemId)
                .WithCount(count)
                .WithQuality(quality)
                .WithSource(source)
                .WithFixedLevel(recipeResult.FixedLevel)
                .WithFixedEnhance(recipeResult.FixedEnhance)
                .Build();
        }
    }

    /// <summary>
    /// Extension methods for easier entry manipulation.
    /// </summary>
    public static class CraftResultEntryExtensions
    {
        public static CraftResultEntry WithQuality(this CraftResultEntry entry, int quality)
        {
            entry.Quality = Mathf.Clamp(quality, 0, 5);
            return entry;
        }

        public static CraftResultEntry WithCount(this CraftResultEntry entry, int count)
        {
            entry.Count = Mathf.Max(1, count);
            return entry;
        }

        public static CraftResultEntry AsCritical(this CraftResultEntry entry, bool isCritical = true)
        {
            entry.IsCritical = isCritical;
            return entry;
        }

        public static CraftResultEntry WithSource(this CraftResultEntry entry, CraftRewardSource source)
        {
            entry.Source = source.ToString();
            return entry;
        }

        public static CraftResultEntry Clone(this CraftResultEntry entry)
        {
            return new CraftResultEntry
            {
                ItemId = entry.ItemId,
                Count = entry.Count,
                Quality = entry.Quality,
                Source = entry.Source,
                IsCritical = entry.IsCritical,
                FixedLevel = entry.FixedLevel,
                FixedEnhance = entry.FixedEnhance,
                SocketCount = entry.SocketCount
            };
        }

        public static List<CraftResultEntry> CloneAll(this List<CraftResultEntry> entries)
        {
            return entries.ConvertAll(e => e.Clone());
        }
    }
}