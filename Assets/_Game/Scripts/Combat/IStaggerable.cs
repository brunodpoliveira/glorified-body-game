namespace GlorifiedBody.Combat
{
    /// <summary>Something a hit can interrupt: an aim, a telegraph, a swing.</summary>
    public interface IStaggerable
    {
        void Stagger(float seconds);
    }
}
