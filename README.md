# LanTransfer 🚀

**局域网文件传输工具** - 一个现代化、易用的 Windows 桌面应用，支持局域网内设备之间快速传输文件。

![Language](https://img.shields.io/badge/Language-C%23-blue.svg)
![Framework](https://img.shields.io/badge/Framework-.NET%208.0-green.svg)
![License](https://img.shields.io/badge/License-MIT-orange.svg)

---

## ✨ 功能特性

- 🌐 **局域网传输** - 在同一局域网内的设备间快速传输文件，无需互联网
- 📱 **二维码连接** - 生成二维码，设备可通过扫码快速连接
- 🔗 **便捷链接码** - 一键生成连接码，其他设备输入即可连接
- 📤 **拖拽上传** - 支持直接拖拽文件/文件夹到应用窗口
- 📥 **文件接收** - 自动接收其他设备发送的文件
- 📊 **实时状态** - 显示连接设备数量和传输状态
- 📋 **传输记录** - 完整的文件收发历史记录
- 🖥️ **设备列表** - 查看所有连接的设备及其 IP 地址
- 📝 **实时日志** - 详细的操作日志便于调试和监控

---

## 🖼️ 界面预览

应用主界面包含：
- **连接信息卡片** - 显示当前设备地址、连接码、二维码和在线设备数
- **拖拽区域** - 拖拽文件/文件夹或点击选择
- **多标签页面**
  - 已发送：已共享的文件列表
  - 已接收：接收到的文件列表  
  - 传输记录：完整的历史记录
  - 设备：当前连接的所有设备
  - 日志：详细的操作日志

---

## 📋 系统要求

| 要求 | 版本 |
|------|------|
| **操作系统** | Windows 10 或更高版本 |
| **.NET Runtime** | .NET 8.0 |
| **处理器** | x64 架构 |

---

## 🚀 快速开始

### 方法一：直接使用（推荐）

1. 下载最新的 `LanTransfer.exe` 执行文件
2. 双击运行，无需安装
3. 选择要传输的文件/文件夹或拖拽到应用
4. 其他设备访问显示的地址或扫描二维码即可接收文件

### 方法二：从源代码编译

```bash
# 克隆仓库
git clone https://github.com/loveherui-star/Transfer.git
cd Transfer

# 编译项目
dotnet build

# 运行应用
dotnet run
```

### 发布单文件版本

```bash
# 在项目根目录运行
.\publish.bat

# 或手动执行
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```

---

## 📱 使用说明

### 基本流程

1. **启动应用**
   - 运行 `LanTransfer.exe`
   - 等待服务启动（自动侦听连接）

2. **分享文件**
   - 方式一：拖拽文件/文件夹到拖拽区域
   - 方式二：点击拖拽区域选择文件或文件夹

3. **其他设备连接**
   - **方式一（推荐）**：用手机或其他设备扫描显示的二维码
   - **方式二**：直接访问显示的 HTTP 地址（如 `http://192.168.1.100:5000`）
   - **方式三**：输入显示的连接码进行连接

4. **接收文件**
   - 其他设备访问后，可选择要接收的文件进行下载
   - 文件自动保存到应用指定的接收文件夹

### 高级操作

- **复制地址** - 点击地址文本即可复制到剪贴板
- **复制连接码** - 点击连接码即可复制
- **打开接收文件夹** - 点击底部"打开接收文件夹"按钮
- **查看设备** - 在"设备"标签页可查看所有连接的设备 IP
- **查看日志** - 在"日志"标签页查看详细的操作记录

---

## 🛠️ 技术栈

| 技术 | 说明 |
|------|------|
| **语言** | C# 11 |
| **UI框架** | WPF (Windows Presentation Foundation) |
| **.NET** | .NET 8.0 |
| **MVVM** | CommunityToolkit.Mvvm |
| **二维码** | QRCoder |
| **HTTP** | ASP.NET Core (内置) |

---

## 📁 项目结构

```
Transfer/
├── MainWindow.xaml          # 主窗口 UI
├── MainWindow.xaml.cs       # 主窗口逻辑
├── App.xaml                 # 应用��置
├── Converters.cs            # 值转换器
├── Services/                # 业务服务层
├── ViewModels/              # MVVM ViewModel
├── Resources/               # 资源文件（图标等）
├── LanTransfer.csproj       # 项目配置
└── publish.bat              # 发布脚本
```

---

## 🔒 隐私和安全

- ✅ 所有文件传输仅在局域网内进行
- ✅ 无需上传到云端或第三方服务器
- ✅ 完全离线运行，保护你的隐私
- ⚠️ 建议只在信任的网络中使用

---

## 📝 版本历史

### v1.0.0 (当前版本)
- ✨ 初始版本
- 🎯 支持基本的文件共享和接收功能
- 📊 实时传输状态显示
- 🔗 二维码和连接码支持

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## ⚙️ 常见问题

### Q: 如何更改文件接收路径？
A: 目前接收路径为固定位置。可以在应用内点击"打开接收文件夹"进行查看。

### Q: 多个设备无法同时连接？
A: 应用支持多个设备同时连接。如遇问题，请检查防火墙设置。

### Q: 传输速度很慢？
A: 传输速度取决于局域网条件。确保设备连接到同一 WiFi 网络。

### Q: 如何关闭应用？
A: 直接关闭窗口即可。应用会自动清理资源。

---

## 📄 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

---

## 👨‍💻 作者

**Harry1999-sky**

---

## 📧 联系方式

有任何问题或建议，欢迎提交 Issue 或 Pull Request！

---

<div align="center">

⭐ 如果这个项目对你有帮助，请给一个 Star 鼓励一下！

</div>
