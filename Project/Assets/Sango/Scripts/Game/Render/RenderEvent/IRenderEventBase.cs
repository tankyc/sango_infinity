namespace Sango.Game.Render
{
    public interface IRenderEventBase
    {
        bool IsVisible();
        bool Update(float deltaTime);
    }
}
