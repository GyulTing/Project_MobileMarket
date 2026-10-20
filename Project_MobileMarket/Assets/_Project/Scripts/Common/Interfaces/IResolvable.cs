namespace MobileMarket.Common
{
    /// <summary>
    /// An interactable that is "done" after the player uses it (fuse installed, door opened, note read). Highlights stop
    /// and prompts can hide once it is resolved.
    /// </summary>
    public interface IResolvable
    {
        bool IsResolved { get; }
    }
}
