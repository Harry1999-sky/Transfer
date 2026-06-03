# LanTransfer v1.2.0 更新说明

## 🌍 中英文国际化

新增完整的中英文切换支持，覆盖所有用户界面文字：

- **WPF 窗口**：标题、标签、按钮、Tab、提示、状态、Tooltip 等 19 处文字全部支持动态切换
- **浏览器端 HTML 页面**：连接码输入页、主操作页、错误页的所有文字和 JavaScript 字符串
- **服务器日志与错误消息**：27 处日志提示、设备名称、错误返回信息
- **对话框**：文件选择、文件夹选择、初始化失败等 5 处 MessageBox
- **语言偏好持久化**：保存到 `%AppData%/LanTransfer/settings.json`，下次启动自动恢复
- **即时切换**：点击右上角 `EN` / `中文` 按钮，UI 即时刷新，无需重启

## 🎨 UI 现代化重构

全面升级为 Windows 11 Fluent Design 风格：

### 连接信息卡片
- 二维码放大至 150×150，成为视觉中心
- 连接码使用 36px Bold 大字号突出显示
- 新增复制按钮（地址、连接码各一个）
- IP 地址弱化处理，扫码连接提示更清晰

### Tab 导航
- 移除默认 WPF TabControl 样式
- 改为现代 Segmented Control 风格（RadioButton 模板）
- 选中态带柔和阴影和圆角高亮

### 文件列表
- 纯文本行升级为卡片式设计
- 每项带文件类型图标、文件名、大小、状态标签
- 取消共享按钮改为图标按钮

### 设备列表
- 新增设备图标系统（自动识别 iPhone / iPad / Android / Mac / Windows / Linux）
- 每个设备品牌使用独立颜色（Apple 灰、Android 绿、Windows 蓝）
- 在线状态用绿色圆点 + "Online" 标签

### 日志界面
- 改为开发者工具风格深色终端
- 使用 Cascadia Code 等宽字体
- 深色背景（#1E1E1E）+ 浅色文字

### 整体视觉
- 统一 8px 间距系统
- 12px 圆角卡片
- 柔和阴影体系（CardShadow / CardShadowHover）
- 5 种按钮风格（Accent / Subtle / Outline / Icon / Danger）
- 现代滚动条样式
- 窗口尺寸优化为 680×800 响应式布局

## 🐛 修复

- 修复语言切换导致程序崩溃的问题（Color 与 Brush 类型不匹配）
- 修复语言切换导致 UI 卡死的问题（ResourceDictionary 原子替换）
- 添加全局异常捕获，防止静默崩溃

## 📦 技术栈

- .NET 8 / WPF / CommunityToolkit.Mvvm
- QRCoder（二维码生成）
- 自实现 HTTP 服务器（基于 TcpListener）
- 自实现 multipart/form-data 解析器
- 单文件自包含绿色版发布

## 📁 项目结构

```
├── App.xaml                    # 应用入口
├── MainWindow.xaml             # 主窗口布局
├── Converters.cs               # 值转换器（7个）
├── LanTransfer.csproj          # 项目配置
├── Localization/               # 国际化
│   ├── LanguageManager.cs      # 语言管理器
│   ├── Strings.zh-CN.xaml      # 中文资源
│   └── Strings.en.xaml         # 英文资源
├── Services/                   # 服务层
│   ├── HttpServerService.cs    # HTTP 服务器
│   ├── HtmlTemplates.cs        # HTML 页面模板
│   ├── MultipartReader.cs      # 文件上传解析
│   └── NetworkHelper.cs        # 网络工具
├── Themes/
│   └── Styles.xaml             # Fluent Design 样式
└── ViewModels/
    └── MainViewModel.cs        # 主视图模型
```
