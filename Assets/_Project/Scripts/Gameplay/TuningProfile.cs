using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Editable asset holding every number that defines how movement/stamina feel.
    /// Select it in the Project window and change values: they apply live, even in Play mode.
    /// (Create more via Assets > Create > FS27 > Tuning Profile.)
    /// </summary>
    [CreateAssetMenu(fileName = "TuningProfile", menuName = "FS27/Tuning Profile")]
    public class TuningProfile : ScriptableObject
    {
        [SerializeField] private MovementTuning movement = new MovementTuning();

        public MovementTuning Movement => movement;
    }
}
