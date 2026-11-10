# WattLite

从 [Watt Toolkit (Steam++)](https://github.com/BeyondDimension/SteamTools) 中剥离出来的轻量游戏加速器。

原版功能很多，但启动慢。这里只保留「游戏加速」这一件事：常驻系统托盘，启动后自动开启加速，退出时自动还原 hosts。

## 功能

- 托盘常驻，启动即自动开启加速（无需手动点）
- 加速域名：Steam / Twitch / Origin / Uplay / Roblox / Nexus Mods / Mod.io / GitHub
- 右键菜单：启动加速 / 停止加速 / 查看日志 / 打开程序目录 / 退出
- 退出时自动停止反向代理、结束加速器子进程并还原 hosts，不留残留
- 提供 Windows 安装包（含开始菜单、桌面快捷方式与卸载功能）

## 工作原理

1. 以 IPC 服务端身份启动官方 `Steam++.Accelerator.exe` 子进程（`dotnetCampus.Ipc`）
2. 通过 `IReverseProxyService.StartProxyAsync` 下发精简后的加速项目（`domains.json`）
3. 子进程监听 80/443 做反向代理，本程序把对应域名写入 hosts 指向 127.0.0.1
4. 停止/退出时调用 `StopProxyAsync` 并删除 hosts 中的标记区块

为保证子进程的 `AssemblyInfo.ValidateAssembly` 版本校验通过，程序集的 Title / Product / Company / Copyright / Description 必须与官方逐字一致。

## 目录

| 路径 | 说明 |
| --- | --- |
| `*.cs` | 主程序源码 |
| `domains.json` | 内置加速规则（由官方 `LOCAL_ACCELERATE.json` 解码后精简） |
| `libs/` | 编译/运行所需的官方 DLL（白名单裁剪） |
| `installer/WattLite.nsi` | NSIS 安装包脚本 |
| `Accelerator/`、`dotnet/` | 构建所需，来自官方安装目录，未纳入版本库 |

## 构建前提

1. 安装 Watt Toolkit（Microsoft Store 版），版本 `3.1.2025.0`
2. 把官方安装目录下的 `Accelerator/` 与 `dotnet/` 复制到本目录

编译所需的官方 DLL（39 个，已裁剪）已提交在 `libs/` 目录，无需另行准备。

## 构建

```powershell
dotnet publish WattLite.csproj -c Release -r win-x64 -o publish
makensis installer\WattLite.nsi
```

## 说明

本项目仅为个人自用工具，依赖 Watt Toolkit 的官方二进制文件（版权归原作者所有），不重新分发其运行库。
