using Sango.Core;

namespace Sango.Render
{
    public class ForceFallCompleteEvent : RenderEventBase
    {
        public Force force;
        Window.WindowInterface targetWindow;

        public override void Enter(Scenario scenario)
        {
            IsDone = false;
            
            string showText = $"{scenario.Info.year}年{scenario.Info.month}月，\n{force.ColorName}势力灭亡了。";
            targetWindow = Window.Instance.Open("window_force_destroy", showText);
            GameMedia.Instance.PauseBgm();
            GameMedia.Instance.PlaySfx(44);
            if (targetWindow == null)
            {
                IsDone = true;
                return;
            }
            targetWindow.ugui_instance.OnCloseAction = OnWindowHide;
        }

        void OnWindowHide()
        {
            GameMedia.Instance.ResumeBgm();
            targetWindow.ugui_instance.OnCloseAction = null;
            IsDone = true;
        }
    }
}
