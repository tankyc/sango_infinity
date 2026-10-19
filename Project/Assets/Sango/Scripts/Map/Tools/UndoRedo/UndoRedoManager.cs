using System.Collections.Generic;
using UnityEngine;

namespace Sango.Tools.UndoRedo
{
    /// <summary>
    /// 撤销/重做管理器
    /// </summary>
    public class UndoRedoManager
    {
        /// <summary>
        /// 撤销历史记录
        /// </summary>
        private Stack<IUndoableCommand> undoStack = new Stack<IUndoableCommand>();
        
        /// <summary>
        /// 重做历史记录
        /// </summary>
        private Stack<IUndoableCommand> redoStack = new Stack<IUndoableCommand>();
        
        /// <summary>
        /// 获取撤销栈（用于操作历史窗口）
        /// </summary>
        public Stack<IUndoableCommand> UndoStack { get { return undoStack; } }
        
        /// <summary>
        /// 获取重做栈（用于操作历史窗口）
        /// </summary>
        public Stack<IUndoableCommand> RedoStack { get { return redoStack; } }
        
        /// <summary>
        /// 命令添加事件
        /// </summary>
        public event System.Action<IUndoableCommand> CommandAdded;
        
        /// <summary>
        /// 历史记录变化事件
        /// </summary>
        public event System.Action HistoryChanged;
        
        /// <summary>
        /// 最大历史记录数量
        /// </summary>
        public int MaxHistoryCount { get; set; } = 100;
        
        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => undoStack.Count > 0;
        
        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => redoStack.Count > 0;
        
        /// <summary>
        /// 添加命令到历史记录
        /// </summary>
        /// <param name="command">要添加的命令</param>
        public void AddCommand(IUndoableCommand command, bool execute = false)
        {
            // 执行命令
            if(execute)
                command.Execute();
            
            // 添加到撤销栈
            undoStack.Push(command);
            
            // 清空重做栈（新操作后无法重做之前的操作）
            // 必须先让命令自己释放资源：创建类命令被撤销后物件已移出地图，
            // 此时只清空引用会让它的 GameObject 变成无人回收的孤儿
            while (redoStack.Count > 0)
            {
                redoStack.Pop().Destroy();
            }
            
            // 限制历史记录数量
            if (undoStack.Count > MaxHistoryCount)
            {
                // 创建新栈并保留最近的记录
                Stack<IUndoableCommand> tempStack = new Stack<IUndoableCommand>();
                int count = 0;
                
                while (undoStack.Count > 0)
                {
                    tempStack.Push(undoStack.Pop());
                    count++;
                    
                    if (count >= MaxHistoryCount)
                    {
                        break;
                    }
                }
                
                // 销毁被舍弃的命令
                while (undoStack.Count > 0)
                {
                    IUndoableCommand cmd = undoStack.Pop();
                    cmd.Destroy();
                }
                
                // 重新填充栈
                while (tempStack.Count > 0)
                {
                    undoStack.Push(tempStack.Pop());
                }
            }
            
            // 触发命令添加事件
            CommandAdded?.Invoke(command);
            
            // 触发历史记录变化事件
            HistoryChanged?.Invoke();
        }
        
        /// <summary>
        /// 撤销操作
        /// </summary>
        public bool Undo()
        {
            if (CanUndo)
            {
                IUndoableCommand command = undoStack.Pop();
                command.Undo();
                redoStack.Push(command);
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke();
                
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 重做操作
        /// </summary>
        public bool Redo()
        {
            if (CanRedo)
            {
                IUndoableCommand command = redoStack.Pop();
                command.Redo();
                undoStack.Push(command);
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke();
                
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void ClearHistory()
        {
            // 销毁所有命令
            while (undoStack.Count > 0)
            {
                IUndoableCommand cmd = undoStack.Pop();
                cmd.Destroy();
            }
            while (redoStack.Count > 0)
            {
                IUndoableCommand cmd = redoStack.Pop();
                cmd.Destroy();
            }
            
            // 触发历史记录变化事件
            HistoryChanged?.Invoke();
        }
        
        /// <summary>
        /// 获取下一个可撤销命令的描述
        /// </summary>
        public string GetNextUndoDescription()
        {
            if (CanUndo)
            {
                return undoStack.Peek().Description;
            }
            return "无法撤销";
        }
        
        /// <summary>
        /// 获取下一个可重做命令的描述
        /// </summary>
        public string GetNextRedoDescription()
        {
            if (CanRedo)
            {
                return redoStack.Peek().Description;
            }
            return "无法重做";
        }
    }
}