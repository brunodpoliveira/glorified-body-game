using Invector.vCharacterController.AI;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// The game speeds that set the pace of the walk and run clips of a T49 class (Bruno, round 179: "Speed up the
    /// clips"). A clip plays at speed / (its ground speed x the class height), so the planted foot keeps its place on
    /// the floor. The speeds stay as they are: Lucy's come from the TankController, the figures' from the Invector AI
    /// (vAIMotor.vMovementSpeed; InvectorEnemyFactory keeps the defaults, walk 2 m/s and run 3 m/s).
    /// The pace is set in the class's animator controller (CreateCharacterAssets). The AI body plays the vendor
    /// animator (InvectorEnemyFactory.AnimatorPath), not the class controller.
    /// </summary>
    public static class ClassPace
    {
        private static readonly vAIMotor.vMovementSpeed Ai = new vAIMotor.vMovementSpeed();

        /// <summary>The walk speed of the Invector AI (vAIMovementSpeed.Walking), m/s.</summary>
        public static float FigureWalkSpeed => Ai.walkSpeed;

        /// <summary>The run speed of the Invector AI (vAIMovementSpeed.Running), m/s.</summary>
        public static float FigureRunSpeed => Ai.runningSpeed;
    }
}
