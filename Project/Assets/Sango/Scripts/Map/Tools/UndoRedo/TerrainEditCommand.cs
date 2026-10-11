using Sango.Render;
using System.Collections.Generic;

namespace Sango.Tools.UndoRedo
{
    /// <summary>
    /// 地形编辑命令
    /// </summary>
    public class TerrainEditCommand : IUndoableCommand
    {
        /// <summary>
        /// 编辑类型
        /// </summary>
        public enum EditType
        {
            Height,      // 高度编辑
            Texture,     // 纹理编辑
            Water,       // 水面编辑
        }

        public Rect rect;
        private MapEditor editor;
        private EditType editType;
        private List<VertexDataChange> changes = new List<VertexDataChange>();
        
        /// <summary>
        /// 顶点数据变化记录
        /// </summary>
        public struct VertexDataChange
        {
            public int x;
            public int y;
            public byte oldValue;
            public byte newValue;
        }
        
        /// <summary>
        /// 命令描述
        /// </summary>
        public string Description { get; private set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="editor">地图编辑器</param>
        /// <param name="editType">编辑类型</param>
        /// <param name="changes">变化记录</param>
        /// <param name="description">命令描述</param>
        /// <param name="rect">影响区域边界</param>
        public TerrainEditCommand(MapEditor editor, EditType editType, List<VertexDataChange> changes, string description, Rect rect)
        {
            this.editor = editor;
            this.editType = editType;
            this.changes = changes;
            this.Description = description;
            this.rect = rect;
        }
        
        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute()
        {
            ApplyChanges(true);
        }
        
        /// <summary>
        /// 撤销命令
        /// </summary>
        public void Undo()
        {
            ApplyChanges(false);
        }
        
        /// <summary>
        /// 重做命令
        /// </summary>
        public void Redo()
        {
            ApplyChanges(true);
        }
        
        /// <summary>
        /// 应用变化
        /// </summary>
        /// <param name="applyNewValue">是否应用新值</param>
        private void ApplyChanges(bool applyNewValue)
        {
            foreach (var change in changes)
            {
                MapData.VertexData vertexData = editor.vertexMapData[change.x][change.y];
                
                switch (editType)
                {
                    case EditType.Height:
                        vertexData.height = applyNewValue ? change.newValue : change.oldValue;
                        break;
                    
                    case EditType.Texture:
                        vertexData.textureIndex = applyNewValue ? change.newValue : change.oldValue;
                        break;
                    
                    case EditType.Water:
                        vertexData.water = applyNewValue ? change.newValue : change.oldValue;
                        break;
                }
                
                editor.vertexMapData[change.x][change.y] = vertexData;
            }
            
            // 更新地形渲染
            UpdateTerrainRender();
        }
        
        /// <summary>
        /// 更新地形渲染
        /// </summary>
        private void UpdateTerrainRender()
        {
            // 重新构建地形
            for (int i = 0; i < editor.map.mapTerrain.terrainCells.Length; i++)
            {
                MapCell cell = editor.map.mapTerrain.terrainCells[i];
                if (cell != null)
                {
                    if (cell.Overlaps(rect))
                    {
                        cell.PrepareDatas(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 销毁命令，在命令被舍弃时调用
        /// </summary>
        public void Destroy()
        {
            // 释放资源
            if (changes != null)
            {
                changes.Clear();
                changes = null;
            }
            editor = null;
        }
    }
}