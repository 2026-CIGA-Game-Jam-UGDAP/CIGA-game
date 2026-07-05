# 失重奥德赛

> **2026 CIGA UGDAP 站参展作品**
>
> 双人本地合作 · 物理绳索解谜 · 太空冒险

---

## 🎮 游戏简介

《失重奥德赛》是一款**双人本地合作冒险解谜游戏**。两名宇航员被一条物理绳索连接在一起，在失重的太空中穿梭于星球与飞船残骸之间，收集零件、修复飞船、共同逃出生天。

**核心体验：一根绳子，两个人，无数种解法。**

- **物理绳索** — 基于 Obi Rope 的实时物理模拟，绳子会拉伸、弯曲、缠绕，每一次拉拽都有真实的力反馈
- **星球吸附** — 喷气背包飞向星球/小行星表面，吸附后在表面行走，头朝外脚朝心，感受真实的太空漫步
- **陨石威胁** — 躲避来袭的陨石，被击中会击飞并拉长绳索
- **合作通关** — 收集飞船零件，两人同时对接飞船，才能启动引擎前往下一关

---

## 操作方式

| 操作 | 玩家 1 | 玩家 2 |
|------|--------|--------|
| 喷气上 | `W` | `↑` |
| 喷气左 | `A` | `←` |
| 喷气右 | `D` | `→` |
| 吸附/脱离 | `Left Shift` | `Enter` |
| 交互/对话 | `Space` |

---

## 技术栈

| 技术 | 说明 |
|------|------|
| **引擎** | Unity 2022.3.62f3 |
| **渲染** | URP (Universal Render Pipeline) |
| **物理绳索** | [Obi Rope](https://obi.virtualmethodstudio.com/) — 基于位置的动力学 (PBD) |
| **动画** | DOTween (补间动画) · TextAnimation Pro · Animator |
| **摄像机** | Cinemachine 2.10.7 |
| **UI** | TextMeshPro · Unity UI |
| **输入** | Unity Legacy Input System |
| **视觉风格** | 奇幻太空 · 青蓝暖色 |

---

## 关卡结构

| 场景 | 说明 |
|------|------|
| `Lobby` | 主菜单 |
| `Level0` | 教学关 — 太空站内部，学习基础操作与基本机制 |
| `Level1` | 太空收集 |
| `Level2` | 进阶挑战 |
| `Level3` | 陨石关卡 |
| `End` | 结局 |

---

## 快速开始

1. 打开 .exe
2. 点击 Play 进入游戏
3. 双人同屏，一个用 WAD，一个用↑←→

---

## 项目结构

```
Assets/
├── Scenes/          # 游戏场景
├── Scripts/         # 游戏逻辑脚本
│   ├── PlayerController.cs   # 玩家移动、喷气、吸附
│   ├── RopeController.cs     # Obi 绳索管理、Pin 约束、断裂检测
│   ├── AnchorPoint.cs        # 圆形锚点
│   ├── PolyAnchorPoint.cs    # 多边形锚点
│   ├── GameManager.cs        # 游戏流程、对话调度、过关条件
│   ├── MeteorManager.cs      # 陨石生成与控制
│   ├── RopeConfig.cs         # 绳索参数配置
│   └── ...
├── Obi/             # Obi Rope 插件
├── Settings/        # URP 渲染配置
└── Resources/       # 预制体、材质、贴图等
```

---

## 视觉风格

- 青蓝色主调 + 暖色点缀
- 星球大气光晕效果
- 全屏后处理：Bloom、Color Grading、Vignette

---

## 制作信息
- **策划**：祁蓝、明月
- **美术**：Leta、悠奇、导学壬斯基、颛薄
- **程序**：Concorde0
- **音效**：Niaka
- **程序**：Concorde0
- **赛事**：2026 CIGA UGDAP 站
- **开发周期**：48 小时 Game Jam

---

## 许可证

本项目为 2026 CIGA UGDAP Game Jam 参赛作品，仅供学习交流。所有权利归原作者所有。

---

<p align="center">
  <sub>Made with in 48 hours · 2026 CIGA UGDAP</sub>
</p>
