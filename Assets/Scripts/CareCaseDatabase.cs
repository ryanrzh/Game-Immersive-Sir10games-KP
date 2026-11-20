using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.APD
{
    [CreateAssetMenu(fileName = "CareCaseDatabase", menuName = "Game APD/Care Case Database")]
    public class CareCaseDatabase : ScriptableObject
    {
        [SerializeField] private List<CareCase> cases = new List<CareCase>();

        public IReadOnlyList<CareCase> Cases => cases;

        public IEnumerable<CareCase> GetByType(CareCaseType type)
            => cases.Where(c => c.CaseType == type);

        public IEnumerable<CareCase> GetByEquipment(PersonalProtectiveEquipment equipment, CareCaseType? type = null)
            => cases.Where(c => c.Equipment == equipment && (type == null || c.CaseType == type));
    }
}