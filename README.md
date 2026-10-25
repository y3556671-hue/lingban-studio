# 灵伴工坊 · Lingban Studio

> A Windows desktop companion (desktop pet) and character studio, written in C# / WPF with
> **zero third-party dependencies**. Import a PNG, set four personality dimensions, and the
> character's reactions and lines change accordingly.

**.NET Framework 4.x · 1,811 lines of C# · self-built test harness: 17 model checks + 21
boundary checks + 26 isolated-window assertions, all passing**

---

## What it is

A small always-on-top character that lives on your desktop. You can:

- pick one of three built-in characters (小猫 / 兔兔 / 软团子), or import your own PNG/JPG
  as a lightly animated sticker
- set the character's **name, vitality, affection, tsundere-ness, catchphrase and size**
- pet it (tap), drag it around, right-click for a menu
- use focus-encouragement mode, live character preview, local saving, and hide/restore

Personality is a rule system, not a script: **different personality settings produce
different head-pat reactions, different drag reactions and different lines**, and the
catchphrase you type is woven into the focus-encouragement dialogue.

## Build and run

```powershell
# requires .NET Framework 4.x on Windows
powershell -ExecutionPolicy Bypass -File Build.ps1
.\Lingban.exe
```

Or download the ready-to-run experience pack (see `docs/`) — unzip it **somewhere you can
write to** (the app stores `data/profile.json` and imported images next to the executable),
then double-click `灵伴工坊.exe`.

## Source layout

```
源码/
  Model.cs         393 lines   role data model, personality, save/load, validation
  PetWindow.cs     389 lines   transparent always-on-top pet window, drag, menu
  PetView.cs       358 lines   character rendering and animations
  StudioWindow.cs  316 lines   the studio UI (name, sliders, import, preview)
  SelfTests.cs     314 lines   the self-built assertion harness
  Program.cs        41 lines   entry point
  Build.ps1                 build script
```

## Testing — the interesting part

There is no test framework here. `SelfTests.cs` contains a small assertion harness
(`CheckCase` / `Require`) and runs **17 model self-checks**, all passing:

```
默认角色可直接使用 · 越界设置与无效外观自动修正 · 异常数值与空文本有可靠默认值
长文本和控制字符不会破坏角色资料 · 复制角色后可以独立修改 · 三项性格设置会改变性格说明
温柔和傲娇角色的摸头反应不同 · 活泼和沉稳角色的拖动反应不同 · 自定义口头禅进入专注陪伴台词
安静设置不改变角色性格 · 回应支持缺省输入且不修改原资料 · 真实 PNG 可以导入并独立保存
伪造图片与超限文件会被拒绝 · 超过尺寸限制的有效图片会被拒绝 · 完整角色存档可以准确读回
保存会修正设置且不修改原角色 · 损坏存档保留原文件并恢复可用默认角色
```

Beyond that, the release pass added **21 model boundary checks** plus a real folder-move
verification, and **26 isolated-window assertions** covering the transparent window, hide /
restore, bubbles, and all three exit paths (hide-then-exit, normal exit, direct close).

The point of writing them this way: the interesting bugs are in the **failure paths** —
a corrupt save file, a PNG that is actually a JPEG renamed, a file over the size limit, a
name containing control characters. A corrupt profile is kept on disk and the app falls
back to a working default character rather than losing the user's data.

Full record in `docs/验证记录.txt` (verification log) and `docs/自动检查.txt` (self-check
output), both in Chinese.

## Deliberate boundaries of this version

- Personality responses are **offline rule-based lines** — no online AI chat, no voice.
- "Focus encouragement" is a line of dialogue; there is **no timer**.
- Only **one character is saved**; there is no community, creative workshop, cloud sync or
  Steam integration.
- Built-in colours and scarves are fixed for now; editing them is planned.
- No auto-start setting; the app does **not** scan your desktop or upload images.
- Image import accepts static PNG/JPG up to 10 MB and 4096 px on each side; the image is
  copied into `data/assets/` next to the program. It renders as a sticker with light motion
  — it does not turn an arbitrary image into an animated model.

## Verification status

Confirmed on a real machine: the studio opens, the sliders take effect immediately, the
character saves, the hide button toggles to the restore entry point, and the process exits
on close.

**Still unverified on real hardware:** manual pet dragging, the complete image-picker
flow, and behaviour across different monitor configurations.

## Also included

`docs/桌宠创作平台-完整产品方案.md` — a 288-line product proposal covering positioning,
competitors, the personality system, memory types, business model and roadmap. It opens by
stating that every price, cost, schedule, performance figure and user metric in it is a
**planning assumption, not a measured result**, and that no user interviews or market-size
estimates were done.

## Author

Meng Yiyang — University of Melbourne, Bachelor of Computer and Software Systems.
Contact: 3641316351@qq.com
