using Sango.Core.Player;

using Sango.Core; namespace Sango.UI
{
    public class UIMobileCancel : UGUIWindow
    {
        public override void OnClose()
        {
            OnCloseAction?.Invoke();
        }

        public void OnCancel()
        {
            GameController.Instance.OnCancel();
        }
    }
}
