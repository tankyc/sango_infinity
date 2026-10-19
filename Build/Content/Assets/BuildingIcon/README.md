# 建筑图标目录（运行时裸文件）

本目录是 `ContentRootPath/Content/Assets/BuildingIcon`，与
`Project/Assets/Mods/Content/Assets/BuildingIcon/` 内容一致，供**开发期热改**使用：
放进来即可生效，不需要重新导出 `Content` 资源包。

- 文件名 = 建筑类型 Id（如 `4.png` 对应「阵」），详见另一份 README。
- 加载：`GameRenderHelper.LoadBuildingIcon(icon)` 第 ① 步（磁盘裸文件）。
- UI 展示用 RawImage + Texture。
- 已包含 52 个建筑图标；兵种图标仍走 `UI/AtlasTexture` 老口径（由该方法兜底）。
- 发布包不使用本目录，改完若要让正式包生效，仍需同步到
  `Project/Assets/Mods/Content/Assets/BuildingIcon/` 并重新导出资源包。
