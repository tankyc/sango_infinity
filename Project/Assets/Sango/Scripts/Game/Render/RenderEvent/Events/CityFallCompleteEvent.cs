using Sango.Core;

namespace Sango.Render
{
    public class CityFallCompleteEvent : RenderEventBase
    {
        public City city;
        Window.WindowInterface targetWindow;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            targetWindow = Window.Instance.Open("window_city_complete", city.Name);
            GameMedia.Instance.PauseBgm();
            GameMedia.Instance.PlaySfx(57);
            if (targetWindow == null)
            {
                IsDone = true;
                return;
            }
            targetWindow.ugui_instance.OnCloseAction = OnWindowHide;
        }

        void OnWindowHide()
        {
            targetWindow.ugui_instance.OnCloseAction = null;
            IsDone = true;
            GameMedia.Instance.ResumeBgm();
        }
    }
}
