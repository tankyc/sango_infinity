using Sango.Render;
using System.Collections.Generic;

namespace Sango.Tools.UndoRedo
{
    /// <summary>
    /// 地格编辑命令
    /// </summary>
    public class GridEditCommand : IUndoableCommand
    {
        /// <summary>
        /// 编辑类型
        /// </summary>
        public enum EditType
        {
            TerrainType,  // 地形类型
            Area,         // 区域
            Interior,     // 内政
            Defence,      // 防守
            Thief,        // 贼
        }
        
        private MapEditor editor;
        private EditType editType;
        private List<GridDataChange> changes = new List<GridDataChange>();
        private GridBrush gridBrush;

        /// <summary>
        /// 地格数据变化记录
        /// </summary>
        public struct GridDataChange
        {
            public int col;
            public int row;
            public int oldValue;
            public int newValue;
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
        public GridEditCommand(MapEditor editor, EditType editType, List<GridDataChange> changes, string description, GridBrush gridBrush)
        {
            this.editor = editor;
            this.editType = editType;
            this.changes = changes;
            this.Description = description;
            this.gridBrush = gridBrush;
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
                MapGrid.GridData gridData = editor.map.mapGrid.GetGridData(change.col, change.row);
                int value = applyNewValue ? change.newValue : change.oldValue;
                
                switch (editType)
                {
                    case EditType.TerrainType:
                        gridData.terrainType = (byte)value;
                        editor.map.mapGrid.BeginUpdateMovable(change.col, change.row);
                        break;
                    
                    case EditType.Area:
                        gridData.areaId = (ushort)value;
                        break;
                    
                    case EditType.Interior:
                        gridData.SetGridState(MapGrid.GridState.Interior, value > 0);
                        break;
                    
                    case EditType.Defence:
                        gridData.SetGridState(MapGrid.GridState.Defence, value > 0);
                        break;
                    
                    case EditType.Thief:
                        gridData.SetGridState(MapGrid.GridState.Thief, value > 0);
                        break;
                }

                // 更新地格数据
                gridBrush.SetTerrainMaskShowColor(change.col, change.row, value, gridBrush.terrainTypeMaskCol, gridBrush.terrainTypeMaskRow);

                //editor.map.mapGrid.SetGridData(change.col, change.row, gridData);
            }

            // 更新地格渲染
            UpdateGridRender();
        }
        
        /// <summary>
        /// 更新地格渲染
        /// </summary>
        private void UpdateGridRender()
        {
            //// 如果是地形类型变化，需要更新可移动性
            //if (editType == EditType.TerrainType)
            //{
            //    // 重新计算可移动性
            //    editor.map.mapGrid.EndUpdateMovable();
            //}
            
            //// 更新地格显示
            //editor.map.mapGrid.ApplyRangMask();

            //// 更新地形掩码显示
            //for (int i = 0; i < tempHexList.Count; i++)
            //{
            //    Sango.Hexagon.Hex h = tempHexList[i];
            //    Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
            //    MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
            //}

            gridBrush.terrainTypeMaskTex.Apply(false);
            editor.map.mapGrid.EndUpdateMovable();


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
            gridBrush = null;
        }
    }
}