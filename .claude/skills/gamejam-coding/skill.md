---
name: gamejam-coding
description: >-
  Constrain AI to write simple, pragmatic, fast-shipping Unity C# code for
  48-hour Game Jams. OVERRIDES normal software engineering best practices —
  no layers, no DI, no event buses, no over-abstraction.
  Activate whenever the user asks to write, modify, or refactor game logic
  (Gameplay, character control, UI interaction, networking, spawning, etc.)
  in a jam / hackathon / prototype context.
  Even if the user doesn't mention "jam" explicitly — when the vibe is
  "make it work fast", use this skill.
---

# 🚀 Game Jam Coding — 务实至上

## 核心理念

> **48 小时后，没人会看你的代码。但大家会玩你的游戏。**

这不是一个教你"写好代码"的 skill — 这是一个教你**在截止日期前把功能跑起来**的 skill。
你在 48 小时内做完一个可玩的游戏，而不是写一个能跑 10 年的系统。

**原则很简单：**
- 能跑的垃圾 > 没写完的优美架构
- 耦合的完整功能 > 解耦的半成品
- 硬编码的爽快体验 > 可配置的抽象骨架

---

## 1. 模块直连，不要解耦

| ❌ 不要 | ✅ 要 |
|---------|------|
| 写一个 EventBus / MessageSystem / Signal 来解耦两个脚本 | 直接用 `FindObjectOfType<PlayerHealth>()` 或拖引用 |
| 写 interface + DI 容器 | `GetComponent<Player>()` 一把梭 |
| 写 Command Pattern 队列 | 直接调方法 — `player.TakeDamage(10)` |

**为什么：** Game Jam 里游戏逻辑改动极快，你拿事件系统绕了一圈，最后发现参数不对还要改三个文件。直连改起来最快。**高耦合在 Jam 里不是技术债，是省时间。**

就算用 Mirror，`NetworkBehaviour` 之间直接 `[Command]` / `[ClientRpc]` 调用，不需要中间层。

---

## 2. SetActive 搞定一切"状态切换"

遇到需要切换行为/表现/UI 的场景：
- 把对象放在场景里，用 `SetActive(true/false)` 控制
- 不要写 FSM（有限状态机）框架
- 不要写 State Pattern
- 不要在 UI 之间搞 Show/Hide 队列

```csharp
// ✅ 可以接受
pausePanel.SetActive(true);
gameUI.SetActive(false);

// ❌ 不要写
// UIManager.Instance.PushState(new PauseState());
// 然后建一个 UIStateMachine，还要注册状态转换表
```

**例外：** 角色自己的移动状态（idle/run/jump）确实需要管理 — 但用 `enum + switch` 就够了，不需要 State Pattern 框架。

---

## 3. 一个功能 ≈ 一个文件

- 不要把功能拆成 `IInputHandler → InputManager → PlayerController → PlayerAnimator → PlayerVFX`
- 一个角色的逻辑：**一个脚本**，最多两个（逻辑 + 表现）

```csharp
// ✅ 武器射击逻辑直接在 Weapon 脚本里
public class Weapon : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate = 0.2f;

    void Update()
    {
        if (Input.GetButtonDown("Fire1") && Time.time > nextFireTime)
            Fire();
    }

    void Fire() { /* 生成子弹、播放动画、音效 */ }
}

// ❌ 不要拆成
// IWeapon → WeaponBase → Pistol : WeaponBase → WeaponManager → WeaponVFX → WeaponAudio
```

**为什么：** 48 小时内你改的是功能，不是层。代码都在一个文件里，改起来少跳转五个 tab。

---

## 4. 魔法数字 > ScriptableObject

| 场景 | ✅ 做法 |
|------|--------|
| 移动速度 5 | `float moveSpeed = 5f;` |
| 血量 100 | `int maxHealth = 100;` |
| 冷却时间 2 秒 | `float cooldown = 2f;` |

**只有两种情况需要提到 Inspector 或 ScriptableObject：**
1. 你要在 Editor 里反复调数值来找手感（平衡性 tuning）— 这时用 `[SerializeField]` 暴露到 Inspector
2. 这个数据必须被预制体/场景引用（子弹预制体、特效）— 用 public 拖引用

不要为了"可配置"去建 Config / Settings / DataSO 体系。

---

## 5. 相似的逻辑 → 复制粘贴

两个武器/敌人/道具行为相似但不完全相同 → 写两个独立脚本，不要抽基类。

```csharp
// ✅ 两个脚本，各自独立
public class Pistol : MonoBehaviour { /* 射击逻辑 */ }
public class Shotgun : MonoBehaviour { /* 也是射击逻辑，数据和方法都独立 */ }

// ❌ 不要抽
// public abstract class GunBase : MonoBehaviour { ... }
// public class Pistol : GunBase { ... }
// public class Shotgun : GunBase { ... }
```

**为什么：** 抽基类 = 你先想清楚"哪些是共用的"，然后把那部分塞进父类，然后子类要 override 一堆 virtual 方法。
直接复制粘贴两份，要改一起改 — Jam 项目总共就几百行代码，重复不是问题，抽象才是。

如果后来发现两个脚本逻辑完全一样，**再**合并都不迟（而且大概率永远不会到那一步）。

---

## 6. 禁用清单 — 这些模式在 Jam 里不要碰

| 模式 | 原因 |
|------|------|
| **MVC / MVVM** | 一个 GameJam 游戏不需要三个层 |
| **Dependency Injection** | `FindObjectOfType` 就是你的 DI 容器 |
| **Event Bus / Message System** | 两个类通信不需要一个邮局 |
| **Command Pattern** | 你不需要 Undo/Redo/Queue |
| **State Machine Framework** | `enum + switch` 就够，别上 `StateMachine<T>` |
| **Builder Pattern** | `new GameObject()` + 设属性比 Builder 快 |
| **Strategy Pattern** | 三个 if 比一个 Strategy 接口好读 |
| **Object Pool（自己写）** | Unity 自带池（用 `Pool`），没有就别加；Jam 里 GC 不是你的敌人 |
| **Factory Pattern** | `Instantiate(prefab)` 就是工厂 |
| **Observer Pattern（自己实现）** | UnityEvent / C# event 够用，不要写 Observable/Subscription 系统 |
| **Singleton 管理器（全局 Manager 类）** | 不要建 `GameManager` / `AudioManager` / `UIManager` —— 直接在需要的地方用 `FindObjectOfType` 或 static 字段 |

---

## 7. 性能 — 先跑起来，再优化

| ❌ 不要 | ✅ 要 |
|---------|------|
| 写 Object Pool "以防万一" | 等真有 GC 卡顿了再加 |
| 用 StringBuilder 拼接所有字符串 | 直接 `+`，又不是每秒拼接 1000 次 |
| 缓存 GetComponent 到 Awake | 直接 `GetComponent` 随手用（真卡了再加缓存） |
| 写复杂的 Culling/LOD 系统 | 对象不多就跑着，对象多了再想 |
| 担心 foreach 的 GC Alloc | `for` 能写就行，foreach 也可以 |

**关键判断：** 你的 Jam 游戏场景里通常 < 100 个活跃对象，Unity 对这规模完全无压力。
等你真遇到性能问题 — 大概率是 Draw Call 或物理，不是你的代码写法。

---

## 8. Mirror 网络 — 务实写法

既然用 Mirror，规则稍有调整，但基调一样：

| ✅ 直接写 | ❌ 不要写 |
|----------|----------|
| `[Command]` / `[ClientRpc]` 直连 | 网络同步的事件系统 / NetworkMessage 路由器 |
| `SyncVar` 同步关键状态 | 把整个游戏状态塞进一个 NetworkBehaviour |
| `NetworkBehaviour` 子类里直接处理逻辑 | 给网络层再加一层抽象 |
| 谁持有数据谁就是 NetworkBehaviour | 把数据抽出去再思考怎么同步 |

**客户端预测 / 回滚 / 插值：** 除非你的游戏是格斗/FPS 且已经碰到同步问题，否则不要碰这些。

---

## 9. Unity 场景/预制体配置 — 不要代码自动创建 GameObject

**脚本只写逻辑，不替用户摆场景。**

| ❌ 不要 | ✅ 要 |
|---------|------|
| 代码里 `new GameObject()` 创建 NetworkManager、Camera、UI 等基础设施 | 让用户在 Editor 里手动创建、拖引用 |
| `AddComponent<KcpTransport>()` 之类运行时装配 | 用户在 Inspector 里挂组件 |
| `Resources.Load()` 加载 prefab 再赋给字段 | 用户在 Inspector 里拖 prefab 引用 |
| 运行时动态拼 UI 层级 | 用户在场景里摆好，代码只控制 `SetActive` |

**原因：**
1. 运行时创建的对象没法在 Inspector 里调参数 — 拖引用、改数值全靠代码硬编码，失去了 Unity 的可视化优势
2. 用户对场景结构有完全掌控，代码偷偷创建对象会让调试变得混乱（"这个 GameObject 从哪来的？"）
3. 代码创建的对象引用关系不透明，后续维护/修改时只能去翻代码

**唯一例外：** 纯运行时临时对象（子弹 `Instantiate`、特效粒子、Mirror 的 Fade Canvas 这种只在特定时机出现又消失的）。这些不属于"场景基础设施"。

**如果某项配置不方便代码做：** 告诉用户在 Editor 里手动配置，给出清晰步骤。不要试图用代码绕过。

---

## 这个 Skill 什么时候可以忽略

以下场景你**正常写代码**（不适用以上约束）：

1. **编辑器工具 / 自动化脚本** — 正常抽象，写干净
2. **构建 / CI / 发布流程** — 按标准做法来
3. **Git 操作** — 正常流程
4. **纯数据格式处理**（JSON、YAML 解析）— 按常规写
5. **用户明确说"写干净点" / "好好架构一下"** — 他可能想复用这段代码

**但默认在游戏逻辑中，以上一条都不是例外。**
当你不确定的时候 — `FindObjectOfType` 和 `SetActive` 基本是正确答案。

---

## 📋 代码自查清单

**每次写/改完游戏逻辑代码后，逐条检查。全部 ✅ 才算通过：**

- [ ] **无多余接口/基类** — 有没有抽不必要的 interface / abstract class？→ 改回具体类直用
- [ ] **无事件系统** — 有没有引入 EventBus、MessageSystem、Signal？→ 改成直调
- [ ] **无状态机框架** — 能不能用 `SetActive` / `enum + switch` 简化？→ 优先用简单方案
- [ ] **无提前优化** — 有没有写 Object Pool、缓存系统、StringBuilder 之类？→ 去掉，出问题再加
- [ ] **文件数量合理** — 一个功能是不是拆到 N 个文件了？→ 合并，最多 2 个文件
- [ ] **无多余配置层** — 有没有把简单数值抽成 SO / Config / Settings？→ 改回硬编码或 `[SerializeField]`
- [ ] **无禁用模式** — 有没有引入禁用清单里的模式？→ 改成允许的做法
- [ ] **网络层无抽象** — Mirror 是否有不必要的 NetworkMessage 路由或网络层包装？→ 改成 `[Command]` / `[ClientRpc]` 直调
- [ ] **无运行时创建基础设施** — 有没有用代码创建 NetworkManager、Camera、UI 等应该在场景里摆的对象？→ 改成用户在 Inspector 手动配置

**提交前快速扫描整个 diff，确认没有漏网的过度工程。**

---

## 总结

写代码时心里默念三遍：

> **"这是 Game Jam，不是生产环境。这个项目 48 小时后就不动了。跑起来就行。"**

每次你想抽一个接口、建一个文件夹、加一个设计模式的时候 — 先停下来问自己：

> **"这个抽象能让我的游戏更好玩吗？还是只是让代码更'干净'？"**

当你不确定某段代码是否"过度工程"时 — 它就是。
