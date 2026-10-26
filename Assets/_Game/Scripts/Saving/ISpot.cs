using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// A place a save starts from: a save point or a checkpoint. The spot id is the object's name (the room builder
    /// gives it). She starts SpawnDistance in front of it, facing away from it.
    /// </summary>
    public interface ISpot
    {
        string SpotId { get; }
        Pose SpawnPose { get; }
        /// <summary>A load starts her here: a checkpoint does not save again at once.</summary>
        void MarkReached();
    }
}
