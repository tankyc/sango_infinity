using Sango.Core.Player;

using Sango.Core; 
namespace Sango.UI
{
    public class UIChoise : UGUIWindow
    {
        public UIChoiseItem item;
        CreatePool<UIChoiseItem> itemPool;
        PlayerChoice playerChoice;

        protected override void Awake()
        {
            itemPool = new CreatePool<UIChoiseItem>(item);
        }

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            playerChoice = objects[0] as PlayerChoice;
            itemPool.Reset();
            for (int i = 0; i < playerChoice.choiceDatas.Length; i++)
            {
                PlayerChoice.ChoiceData choiceData = playerChoice.choiceDatas[i];
                UIChoiseItem uIChoiseItem = itemPool.Create();
                uIChoiseItem.SetText(choiceData.lab).SetClickCall(OnClickItem).index = i;
            }
        }

        void OnClickItem(UIChoiseItem item)
        {
            playerChoice.OnPlayerChoose(item.index);
        }

    }
}
