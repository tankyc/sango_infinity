using Sango.Core.Player;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 玩家消息窗口UI控制器
    /// 负责管理消息窗口的显示和交互
    /// </summary>
    public class UIPlayerMessage : UGUIWindow
    {
        /// <summary>
        /// 窗口根对象
        /// </summary>
        public GameObject root;

        /// <summary>
        /// 文本消息项预制体
        /// </summary>
        public UITextMessageItem textMessageItem;

        /// <summary>
        /// 人物消息项预制体
        /// </summary>
        public UIPersonMessageItem personMessageItem;

        /// <summary>
        /// 文本消息滚动视图
        /// </summary>
        public LoopVerticalScrollRectMulti textMessageScrollRect;

        /// <summary>
        /// 人物消息滚动视图
        /// </summary>
        public LoopVerticalScrollRectMulti personMessageScrollRect;

        /// <summary>
        /// 文本消息项对象池
        /// </summary>
        private CreatePool<UITextMessageItem> textMsgItemPool;

        /// <summary>
        /// 人物消息项对象池
        /// </summary>
        private CreatePool<UIPersonMessageItem> personMsgItemPool;

        /// <summary>
        /// 文本消息滚动数据源
        /// </summary>
        private TextMessageScroll textMessageScroll;

        /// <summary>
        /// 人物消息滚动数据源
        /// </summary>
        private PersonMessageScroll personMessageScroll;

        /// <summary>
        /// 玩家消息系统实例
        /// </summary>
        private PlayerMessage playerMessage;

        /// <summary>
        /// 池根节点
        /// </summary>
        public RectTransform poolRect;

        /// <summary>
        /// 需要更新显示
        /// </summary>
        bool needUpdateTextView = false;
        bool needUpdatePersonView = false;


        /// <summary>
        /// 文本消息滚动数据源类
        /// 负责文本消息项的创建、回收和数据绑定
        /// </summary>
        private class TextMessageScroll : LoopScrollPrefabSource, LoopScrollMultiDataSource
        {
            /// <summary>
            /// UIPlayerMessage实例引用
            /// </summary>
            internal UIPlayerMessage uIPlayerMessage;

            /// <summary>
            /// 文本消息项对象池
            /// </summary>
            internal CreatePool<UITextMessageItem> pool;

            /// <summary>
            /// 获取消息项对象
            /// </summary>
            /// <param name="index">消息索引</param>
            /// <returns>消息项游戏对象</returns>
            public GameObject GetObject(int index)
            {
                //if (index < 0 || index >= uIPlayerMessage.playerMessage.textMessages.Count)
                //    return null;

                UITextMessageItem uITextMessageItem = pool.Create();
                uITextMessageItem.onClickItem = uIPlayerMessage.OnClickTextMessage;
                return uITextMessageItem.gameObject;
            }

            /// <summary>
            /// 回收消息项对象
            /// </summary>
            /// <param name="trans">消息项变换组件</param>
            public void ReturnObject(Transform trans)
            {
                UITextMessageItem uITextMessageItem = trans.GetComponent<UITextMessageItem>();
                if (uITextMessageItem != null)
                {
                    pool.Recycle(uITextMessageItem, uIPlayerMessage.poolRect);
                }
            }

            /// <summary>
            /// 为消息项提供数据
            /// </summary>
            /// <param name="transform">消息项变换组件</param>
            /// <param name="idx">消息索引</param>
            public void ProvideData(Transform transform, int idx)
            {
                if (idx < 0 || idx >= uIPlayerMessage.playerMessage.textMessages.Count)
                {
                    return;
                }
                UITextMessageItem uITextMessageItem = transform.GetComponent<UITextMessageItem>();
                PlayerMessage.TextMessage data = uIPlayerMessage.playerMessage.textMessages[idx];
                uITextMessageItem.SetDate(data.year, data.month, data.day).SetForce(data.force).SetContent(data.text);
                //uITextMessageItem.SetHeight(data.year > 0 ? 42 : 24);
                uITextMessageItem.textMessage = data;
            }
        }

        private class TextSizeHelper : LoopScrollSizeHelper
        {
            internal UIPlayerMessage uIPlayerMessage;
            public Vector2 GetItemsSize(int itemsCount)
            {
                if (itemsCount <= 0) return new Vector2();
                int count = uIPlayerMessage.playerMessage.textMessages.Count;
                Vector2 sum = new Vector2();
                for (int i = 0; i < count; i++)
                {
                    if (itemsCount <= i) break;
                    int t = (itemsCount - 1 - i) / count + 1;
                    sum += t * GetCellPreferredSize(i);
                }
                return sum;
            }

            Vector2 GetCellPreferredSize(int idx)
            {
                PlayerMessage.TextMessage data = uIPlayerMessage.playerMessage.textMessages[idx];
                if (data.year > 0)
                {
                    return new Vector2(uIPlayerMessage.textMessageScrollRect.preferredWidth, 42);
                }
                else
                {
                    return new Vector2(uIPlayerMessage.textMessageScrollRect.preferredWidth, 24);
                }
            }
        }


        /// <summary>
        /// 人物消息滚动数据源类
        /// 负责人物消息项的创建、回收和数据绑定
        /// </summary>
        private class PersonMessageScroll : LoopScrollPrefabSource, LoopScrollMultiDataSource
        {
            /// <summary>
            /// UIPlayerMessage实例引用
            /// </summary>
            internal UIPlayerMessage uIPlayerMessage;

            /// <summary>
            /// 人物消息项对象池
            /// </summary>
            internal CreatePool<UIPersonMessageItem> pool;

            /// <summary>
            /// 获取消息项对象
            /// </summary>
            /// <param name="index">消息索引</param>
            /// <returns>消息项游戏对象</returns>
            public GameObject GetObject(int index)
            {
                // 【不能返回 null】插件侧 LoopScrollRectMulti.GetFromTempPool 会对返回值直接取 .transform
                // （LoopScrollRectMulti.cs:24），返回 null 必然抛 NullReferenceException。
                // 越界索引由 ProvideData 内部拦截（不填数据，项留空显示），
                // 但对象本身必须给出来 —— 与 TextMessageScroll.GetObject 保持同一约定。
                UIPersonMessageItem uITextMessageItem = pool.Create();
                return uITextMessageItem.gameObject;
            }

            /// <summary>
            /// 回收消息项对象
            /// </summary>
            /// <param name="trans">消息项变换组件</param>
            public void ReturnObject(Transform trans)
            {
                UIPersonMessageItem uITextMessageItem = trans.GetComponent<UIPersonMessageItem>();
                if (uITextMessageItem != null)
                    pool.Recycle(uITextMessageItem);
            }

            /// <summary>
            /// 为消息项提供数据
            /// </summary>
            /// <param name="transform">消息项变换组件</param>
            /// <param name="idx">消息索引</param>
            public void ProvideData(Transform transform, int idx)
            {
                if (idx < 0 || idx >= uIPlayerMessage.playerMessage.personMessages.Count)
                    return;
                UIPersonMessageItem uITextMessageItem = transform.GetComponent<UIPersonMessageItem>();
                PlayerMessage.PersonMessage data = uIPlayerMessage.playerMessage.personMessages[idx];
                uITextMessageItem.SetData(data.text, data.person);
            }
        }

        private class PersonSizeHelper : LoopScrollSizeHelper
        {
            internal UIPlayerMessage uIPlayerMessage;
            public Vector2 GetItemsSize(int itemsCount)
            {
                if (itemsCount <= 0) return new Vector2();
                int count = uIPlayerMessage.playerMessage.personMessages.Count;
                Vector2 sum = new Vector2();
                for (int i = 0; i < count; i++)
                {
                    if (itemsCount <= i) break;
                    int t = (itemsCount - 1 - i) / count + 1;
                    sum += t * GetCellPreferredSize(i);
                }
                return sum;
            }

            Vector2 GetCellPreferredSize(int idx)
            {
                PlayerMessage.PersonMessage data = uIPlayerMessage.playerMessage.personMessages[idx];
                return new Vector2(uIPlayerMessage.textMessageScrollRect.preferredWidth, 80);
            }
        }

        /// <summary>
        /// 窗口唤醒方法
        /// 初始化对象池和滚动数据源
        /// </summary>
        protected override void Awake()
        {
            textMsgItemPool = new CreatePool<UITextMessageItem>(textMessageItem);
            personMsgItemPool = new CreatePool<UIPersonMessageItem>(personMessageItem);
            textMessageScroll = new TextMessageScroll() { uIPlayerMessage = this, pool = textMsgItemPool};
            personMessageScroll = new PersonMessageScroll() { uIPlayerMessage = this, pool = personMsgItemPool };

            textMessageScrollRect.prefabSource = textMessageScroll;
            textMessageScrollRect.dataSource = textMessageScroll;
            textMessageScrollRect.sizeHelper = new TextSizeHelper() { uIPlayerMessage = this };
            personMessageScrollRect.prefabSource = personMessageScroll;
            personMessageScrollRect.dataSource = personMessageScroll;
            personMessageScrollRect.sizeHelper = new PersonSizeHelper() { uIPlayerMessage = this };

        }

        /// <summary>
        /// 窗口显示方法
        /// 获取消息系统实例，更新滚动视图，并注册事件监听
        /// </summary>
        public override void OnOpen()
        {
            base.OnOpen();
            playerMessage = GameSystem.GetSystem<PlayerMessage>();

            textMessageScrollRect.totalCount = playerMessage.textMessages.Count;
            personMessageScrollRect.totalCount = playerMessage.personMessages.Count;

            playerMessage.onTextMessageAdd += OnTextMessageAdd;
            playerMessage.onPersonMessageAdd += OnPersonMessageAdd;

            playerMessage.onVisibleChange += OnMessagePlaneVisible;
        }

        public override void OnClose()
        {
            playerMessage.onTextMessageAdd -= OnTextMessageAdd;
            playerMessage.onPersonMessageAdd -= OnPersonMessageAdd;

            playerMessage.onVisibleChange -= OnMessagePlaneVisible;
            base.OnClose();
        }

        /// <summary>
        /// 消息窗口可见性变化回调
        /// </summary>
        /// <param name="visible">是否可见</param>
        private void OnMessagePlaneVisible(bool visible)
        {
            root.SetActive(visible);
        }

        /// <summary>
        /// 文本消息添加回调
        /// 更新文本消息滚动视图
        /// </summary>
        /// <param name="msg">新添加的文本消息</param>
        /// <param name="message">消息系统实例</param>
        private void OnTextMessageAdd(PlayerMessage.TextMessage msg, PlayerMessage message)
        {
            if (!needUpdateTextView)
            {
                needUpdateTextView = true;
                Invoke("UpdateTextView", 0.1f);
            }
        }

        void UpdateTextView()
        {
            textMessageScrollRect.totalCount = playerMessage.textMessages.Count;
            textMessageScrollRect.RefillCellsFromEnd(0);
            needUpdateTextView = false;
            //Invoke("ToEndTextView", 0.01f);
        }

        void ToEndTextView()
        {
            textMessageScrollRect.ScrollToCell(textMessageScrollRect.totalCount - 1, 99999);
        }

        /// <summary>
        /// 人物消息添加回调
        /// 更新人物消息滚动视图
        /// </summary>
        /// <param name="msg">新添加的人物消息</param>
        /// <param name="message">消息系统实例</param>
        private void OnPersonMessageAdd(PlayerMessage.PersonMessage msg, PlayerMessage message)
        {
            if (!needUpdatePersonView)
            {
                needUpdatePersonView = true;
                Invoke("UpdatePersonView", 0.1f);
            }
        }

        void UpdatePersonView()
        {
            personMessageScrollRect.totalCount = playerMessage.personMessages.Count;
            // 【必须刷自己】原先这里误写成了 textMessageScrollRect：
            // 人物列表的 totalCount 变大了、内部索引区间却没重建，
            // 拖动时 LoopScrollRectMulti.NewItemAtStart 就会用越界的 itemIdx 取对象，
            // PersonMessageScroll.GetObject 判越界返回 null，插件取 .transform 直接空引用。
            personMessageScrollRect.RefillCellsFromEnd();
            needUpdatePersonView = false;
            //Invoke("ToEndPersonView", 0.01f);
        }

        void ToEndPersonView()
        {
            // 同理：人物列表滚到末尾，取的是人物列表的 totalCount，不是文本列表的
            personMessageScrollRect.ScrollToCell(personMessageScrollRect.totalCount - 1, 99999);
        }

        /// <summary>
        /// 最小化按钮点击事件
        /// 隐藏消息窗口
        /// </summary>
        public void OnMinButton()
        {
            playerMessage.onVisibleChange?.Invoke(false);
        }

        /// <summary>
        /// 文本消息点击事件
        /// 将相机移动到消息关联的坐标位置
        /// </summary>
        /// <param name="msg">被点击的文本消息</param>
        private void OnClickTextMessage(PlayerMessage.TextMessage msg)
        {
            if (msg != null)
            {
                if(msg.x > 0 &&  msg.y > 0)
                {
                    Cell cell = Scenario.Cur.Map.GetCell(msg.x, msg.y);
                    if (cell != null)
                        MapRender.Instance.MoveCameraTo(cell.Position, 0.3f);
                }
            }
        }
    }
}
