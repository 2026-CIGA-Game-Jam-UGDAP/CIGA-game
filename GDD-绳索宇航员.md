# 绳索宇航员 — 设计文档 & 实现计划

> 48h Game Jam | Unity 2022.3 + C# | 横版跳跃 2D 双人本地合作

---

## 一、核心概念

两个宇航员被 Obi 弹力绳拴在一起，在失重太空站外协同移动。从起点到终点，避开障碍。

**一句话**：被绳子拴在一起的两人横版跳跃。

---

## 二、设计决策（经 grill-me 拷问）

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 绳索手感 | **弹力绳**（3-5 单位，中高弹性） | 拉远→蓄力→弹回，操作空间大 |
| 玩家操作 | **纯物理涌现** | MVP 不加特殊技能，靠惯性+绳索自发产生配合 |
| 失败处理 | **秒重来** | 参考 Celeste，0.3s 淡出→重置→淡入 |
| 视觉风格 | **极简几何** | 圆形头 + 矩形身体 + 细线绳，2h 出全部美术 |
| 网络方案 | **去 Mirror** | 纯本地 WASD + 方向键，一台电脑两人玩 |
| 摄像机 | **跟随中点 + 自适应 zoom** | 保证两人+绳始终在视野内 |

## 三、设计四柱验证

| 柱子 | 判断 | 要点 |
|------|------|------|
| 低认知压力 | ✅ | 看一眼就知道：俩人被绳拴着，要跳过去 |
| 可迭代 | ✅ | 核心闭环 = 俩宇航员 + 绳 + 平台 + 障碍，独立可玩 |
| 好玩 | ⚠️ Obi 调参 | 弹力绳手感是唯一乐趣来源，暴露参数为 SO 实时调 |
| 循环性 | ⏭️ 跳过 | MVP 不追求 replay |

---

## 四、文件结构

```
Assets/
├── Scripts/
│   ├── PlayerController.cs      # 双人输入 + Rigidbody2D 惯性移动 + 跳跃
│   ├── RopeController.cs        # Obi 绳索管理 + 参数热加载
│   ├── RopeConfig.cs            # 绳索参数 ScriptableObject
│   ├── GameManager.cs           # 游戏流程（过关/失败/重来）
│   ├── CameraFollow.cs          # 双人中点跟随 + 自适应 zoom
│   └── SceneLoader.cs           # 场景切换（已去 Mirror）
├── _Archive/
│   ├── SceneNetworkManager.cs   # 废弃 Mirror
│   └── LobbyUI.cs               # 废弃 Mirror
├── ScriptableObjects/
│   └── RopeConfig.asset         # 绳索参数（Play Mode 可调）
├── Scenes/
│   └── GamePlay.unity           # 游戏场景
└── Resources/
    └── Player.prefab            # 玩家 Prefab
```

---

## 五、实现步骤

### Step 1: 切割 Mirror ✅

- 重写 `PlayerController.cs`：NetworkBehaviour → MonoBehaviour
- 删除 SyncVar、OnStartServer、OnStartClient、OnStartLocalPlayer
- `SceneNetworkManager.cs` → `_Archive/`
- `LobbyUI.cs` → `_Archive/`
- `SceneLoader.cs`：直接 `SceneManager.LoadScene`

### Step 2: 横版移动 ✅

- P1: A/D 水平 + W/Space 跳跃
- P2: ← → 水平 + ↑/Keypad0 跳跃
- `Rigidbody2D` + `drag` 惯性漂移（松键后保持速度）
- `OverlapCircle` 地面检测

### Step 3: Obi 绳索 🔧

- 场景中 Obi Rope（Solver + Blueprint）
- 两端 `ObiParticleAttachment` 绑定 P1/P2 Transform
- `RopeConfig.asset`：`ropeLength` / `stretchStiffness` / `bendingStiffness`
- `RopeController.cs`：Play Mode 实时读取参数
- **第一小时终点：两人 + 绳 + 弹力手感调好**

### Step 4: 摄像机

- 跟随两玩家中点
- 自适应 zoom：距离越大 cam 越远
- X 轴优先（横版）

### Step 5: Game Loop

- 起点 + 终点 Trigger
- 两人到终点 → 过关
- Y < -10 掉落 / 绳断 → 秒重来
- DoTween 淡入淡出

### Step 6: 关卡搭建

- 3 个递增难度区域
- Unity 内置 Sprite + 调色 = 平台/障碍
- 玩家 Prefab：Capsule Collider 2D + 圆形头 Sprite + Rigidbody2D

### Step 7: Juice

- DoTween 过关动画
- 星空背景粒子
- 喷气粒子

---

## 六、时间估算

| Step | 内容 | 时间 |
|------|------|------|
| 1 | 切割 Mirror | 5 min |
| 2 | 横版移动 | 30 min |
| 3 | Obi 绳索 | 60 min |
| 4 | 摄像机 | 20 min |
| 5 | Game Loop | 30 min |
| 6 | 关卡搭建 | 60 min |
| 7 | Juice | 30 min |
| **总计** | | **~4h** |

余量 4h：Obi 调参 + 手感打磨 + debug

---

## 七、验证清单

- [ ] P1 A/D/W — 惯性漂移 + 跳跃
- [ ] P2 ←/→/↑ — 同上
- [ ] 两人拉开距离 — 绳索拉伸/弹回
- [ ] 一人跳下平台 — 绳拉住/弹回
- [ ] 两人掉出屏幕 — 秒重来
- [ ] 两人到终点 — 过关
