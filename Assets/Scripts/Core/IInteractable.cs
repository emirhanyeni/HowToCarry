namespace HowToCarry.Core
{
    public interface IInteractable
    {
        string GetInteractionPrompt();
        void Interact();
    }
}