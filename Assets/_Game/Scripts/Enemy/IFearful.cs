namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The mind that EnemyMorale talks to when a colleague dies (design §11.4 and §11.5): the soldier's brain and
    /// the civilian's brain. Alert: it knows where she is. Shock: it stops and flinches. Retreat: it runs and cowers.
    /// </summary>
    public interface IFearful
    {
        bool Retreating { get; }
        void Alert(string reason, bool callOthers);
        void Shock(float seconds, bool gruesome);
        void Retreat(string reason);
    }
}
