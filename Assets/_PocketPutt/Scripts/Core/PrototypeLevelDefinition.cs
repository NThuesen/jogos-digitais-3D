using System;
using UnityEngine;

namespace PocketPutt.Prototype
{
    // Lightweight authoring data for greybox levels; not a general-purpose level engine.
    [Serializable]
    public sealed class PrototypeLevelDefinition
    {
        public string id;
        public string displayName;
        public int threeStarMaxShots;
        public int twoStarMaxShots;
        public LevelPoint ballStart;
        public LevelPoint hole;
        public LevelBlock[] floors;
        public LevelBlock[] walls;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Level id is required.");

            if (threeStarMaxShots < 1 || twoStarMaxShots <= threeStarMaxShots)
                throw new ArgumentException("Star targets must satisfy 1 <= threeStar < twoStar.");

            if (floors == null || floors.Length == 0 || walls == null || walls.Length == 0)
                throw new ArgumentException("Level requires at least one floor and wall.");

            ValidateBlocks(floors, "floor");
            ValidateBlocks(walls, "wall");
        }

        private static void ValidateBlocks(LevelBlock[] blocks, string type)
        {
            foreach (LevelBlock block in blocks)
            {
                if (block.width <= 0f || block.length <= 0f)
                    throw new ArgumentException($"Invalid {type} dimensions.");
            }
        }
    }

    [Serializable]
    public struct LevelPoint
    {
        public float x;
        public float z;
    }

    [Serializable]
    public struct LevelBlock
    {
        public float x;
        public float z;
        public float width;
        public float length;
        public float rotation;
    }
}
