# Webfishing Chinese Voice

一个 WEBFISHING 的 GDWeave Mod，用来修复中文聊天时没有说话音效、头顶气泡不显示中文的问题。

## 功能

- 中文聊天会按拼音触发 WEBFISHING 原本的说话音效。
- 中文头顶气泡会切换到游戏内置的 Unifont 字体，从而正常显示中文。
- 英文聊天保持原版表现。
- 只修改本地客户端脚本，不改游戏 exe 和存档。

## 安装

你需要先安装 GDWeave。

把整个 `Melon.ChinesePinyinVoice` 文件夹放到：

```text
WEBFISHING/GDWeave/mods/Melon.ChinesePinyinVoice
```

安装后目录应类似这样：

```text
WEBFISHING/
  GDWeave/
    mods/
      Melon.ChinesePinyinVoice/
        manifest.json
        Melon.ChinesePinyinVoice.dll
        Melon.ChinesePinyinVoice.deps.json
        pinyin_chars.txt
        pinyin_syllables.txt
```

然后正常启动游戏即可。

## 卸载

删除这个文件夹即可：

```text
WEBFISHING/GDWeave/mods/Melon.ChinesePinyinVoice
```

## 多人说明

- 你自己装了 Mod 后，可以看到自己和别人发出的中文头顶气泡。
- 没装 Mod 的玩家大概率仍然看不到头顶中文气泡，因为他们本地客户端没有中文字体补丁。
- 中文聊天音效会被转换成拼音字母触发，并通过游戏原本的说话同步发送；其他玩家通常也能听到你的说话音效。
- 聊天框里的中文文本仍走游戏原本聊天逻辑。

## 兼容性

当前 Mod 适配的是 WEBFISHING 的 Godot 3.5.2 版本脚本结构。

它会 patch 这两个脚本：

```text
res://Scenes/Entities/Player/player.gdc
res://Scenes/Entities/Player/SpeechBubble/speech_bubble.gdc
```

如果 WEBFISHING 后续更新大幅修改聊天、玩家发声或气泡脚本，本 Mod 可能需要重新适配。

## 构建

需要 .NET 8 SDK，并且本机已安装 GDWeave。

PowerShell 示例：

```powershell
$env:GDWeavePath = "D:\steam\steamapps\common\WEBFISHING\GDWeave"
dotnet build .\src\Melon.ChinesePinyinVoice.csproj -c Release
```

构建产物会输出到 Mod 根目录：

```text
Melon.ChinesePinyinVoice.dll
Melon.ChinesePinyinVoice.deps.json
Melon.ChinesePinyinVoice.pdb
```

## 文件说明

- `manifest.json`：GDWeave Mod 描述文件。
- `Melon.ChinesePinyinVoice.dll`：Mod 主文件。
- `pinyin_chars.txt`：可识别的中文字符表。
- `pinyin_syllables.txt`：对应的拼音表。
- `src/`：C# 源码。

## 备注

这个 Mod 只是为了让中文聊天体验更自然。它不会翻译文本，也不会改变服务器聊天内容。
