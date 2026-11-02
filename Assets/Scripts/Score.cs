using UnityEngine;

namespace Game
{
    // Hasil satu level: poin, poin maksimal, persen, bintang, lulus atau tidak.
    [System.Serializable]
    public class Score
    {
        [SerializeField] private float point;
        [SerializeField] private float maxPoint;
        [SerializeField] private int star;
        [SerializeField] private bool passed;

        public float Point => point;
        public float MaxPoint => maxPoint;
        public int Star => star;
        public bool Passed => passed;
        public float Percent => maxPoint > 0 ? point / maxPoint * 100f : 0f;

        public Score() { }

        public Score(float point, float maxPoint, ScoreRule rule)
        {
            this.point = Mathf.Max(0f, point); // skor tidak negatif
            this.maxPoint = maxPoint;
            star = rule.GetStar(Percent);
            passed = rule.IsPassed(Percent);
        }
    }
}
