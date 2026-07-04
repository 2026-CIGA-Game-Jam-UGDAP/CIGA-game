# 绳子 + 吸附走路 — 当前实现总结

## 一、吸附走路

### 组件
- **AnchorPoint** — 锚点表面（圆形 CircleCollider2D / 多边形 PolygonCollider2D / 方形 BoxCollider2D）
- **PlayerController** — 玩家移动逻辑，锚点吸附/脱离

### 流程
1. 玩家进入 AnchorPoint 的 trigger → `playersInRange.Add(pc)`
2. 按下吸附键（P1=LeftShift, P2=Enter） → `PlayerController.AttachToAnchor(anchor, speed)`
3. `AttachRoutine` 协程：SmoothStep 飞到表面最近点 → 到达后设 `isAnchored = true`
4. `FixedUpdate` 锚点分支：
   - 读左右输入 → `surfaceT += moveDir * moveSpeed * fixedDeltaTime`
   - `anchor.GetSurfacePoint(surfaceT)` 获取世界位置
   - `rb.MovePosition(targetPos)` 瞬移到表面
   - 旋转跟随表面法线（头朝外）

### 表面数据结构（统一圆/多边形）
- `sampledPoints[]` / `sampledNormals[]` — 均匀采样点（间距 0.1 单位）
- `accumulatedT[]` — 每个采样点的精确周长距离，二分查找 O(logN)
- `surfaceT` 沿周长递增，`WrapT()` 处理环绕

---

## 二、绳子逻辑

### 组件
- **ObiRope** — Obi 物理绳子（粒子模拟 + 约束求解）
- **RopeController** — 运行时初始化 + Pin 约束 + 弹簧拉力 + 粒子同步
- **RopeConfig** — ScriptableObject 参数配置

### 运行时初始化（RopeController.Start）
1. 等一帧 → 生成绳子物理表示（如未初始化）
2. ObiSolver 设零重力、距离迭代 10、弯曲迭代 5
3. 绳子弯曲/拉伸刚度拉满（1.0）→ 太空绷直线
4. **冻结两个玩家**（isKinematic = true）→ 绳子在两人之间自然就位
5. `SetupPins()` — 粒子 0 pin 到 P1，粒子 N pin 到 P2
6. 记录 `fixedRopeLength` = 当前玩家间距 ← **之后永不改变**
7. 等 0.2s + 一次 FixedUpdate → **解冻玩家**，归零残留速度

### Obi 碰撞最小化（SwapToTinyCollider）
- 玩家 ObiCollider2D 的 SourceCollider 从 Capsule 换成 r=0.01 的微型 CircleCollider2D
- 设 Phase=2（绳子粒子 group=1，1≠2 → 自然不碰撞）
- 只影响 Obi 碰撞，不影响 Unity 物理（CapsuleCollider2D 保留）

### 弹簧拉力（RopeController.FixedUpdate）
```
dist = Distance(p1, p2)
if dist > fixedRopeLength:
    force = (dist - fixedRopeLength) * stretchStiffness * 20
    rb1.AddForce(dir * force)
    rb2.AddForce(-dir * force)
```
双向弹簧力，`stretchStiffness` 控制硬度。

### 粒子同步（RopeController.LateUpdate）
直接设置端粒子（0 和 N-1）的位置到玩家位置 + pinOffset，速度归零。
目的：消除 Obi solver 对 `MovePosition`/`velocity` 变化的 1-2 帧延迟。

---

## 三、喷气移动

### 当前方式：冲量式
```
Update:  GetKeyDown 捕获方向 → pendingJetImpulse
FixedUpdate: rb.AddForce(direction * jetForce, ForceMode2D.Impulse)
```
- 每按一下 = 一次冲量（叠加到当前速度上）
- `linearDrag = 0.05` → 松手后慢慢漂停
- 无硬限速，drag 自然限速

### 四方向映射
| 按键 | 方向 |
|------|------|
| W/Up | `transform.up`（朝脸方向） |
| A/Left | `-transform.right`（左） |
| D/Right | `transform.right`（右） |

---

## 四、绳子 + 吸附的交互现状

### 吸附行走时
- **rb.MovePosition** 设绝对位置 → 绳子弹簧力加到 rb 上但下一帧被覆盖 → 绳子拉不住
- 碰碰车：两个吸附玩家相撞 → 沿表面推开（BumpOnSurface）

### 自由飞行时
- AddForce 冲量 + 绳子弹簧 AddForce + Obi 粒子微碰撞 → 三股力叠加
- 非喷气状态下会因为绳子粒子挤压/Obi solver 微力而缓慢漂移

---

## 五、踩坑记录：为什么"改 velocity 直设 + LateUpdate 硬拉"不行

### 尝试的改动
1. 喷气从 `AddForce` 改为 `rb.velocity = direction * jetForce`（直接覆盖速度）
2. 绳子拉力从 `FixedUpdate AddForce` 改为 `LateUpdate MovePosition`（位置硬拉）
3. 绳子 filter 试图关 Obi 碰撞

### 失败原因
- **`rb.MovePosition` 写在 `LateUpdate` 是致命的**：Unity 规定 MovePosition 只能在 FixedUpdate 调用。LateUpdate 运行时物理步已完成，此时改位置导致 Rigidbody2D 内部状态损坏，下一帧物理引擎基于错乱数据计算，玩家被"吸住"完全动不了。
- 搬回 FixedUpdate 后，velocity 直设和 MovePosition 的执行顺序不可控（需要 `[DefaultExecutionOrder]`），且两个玩家 + 绳子的三方位置博弈仍然复杂。
- 喷气 velocity 覆盖 → 绳子弹簧 AddForce 的效果在同一物理步内被覆盖，绳子形同虚设。

### 教训
- **不要试图绕过 Unity 物理管线**。Rigidbody2D + Obi + 自定义力的组合，正确做法是在同一物理阶段内协作，而不是在不同阶段互相覆盖。
- 绳子对吸附的约束应该在**玩家移动逻辑内部**就地处理（移动前检查绳长），而不是外部事后拉回。
- Obi 绳子物理（粒子碰撞、约束求解）和玩家自定义移动之间的冲突，应该用 **Phase 碰撞过滤** 物理层解决，而不是用代码覆盖位置。

---

## 六、正确方向（待实施）

### 绳子约束吸附走路
在 `PlayerController.FixedUpdate` 锚点分支内，计算候选 `surfaceT` 对应的世界位置，检验与另一个玩家的距离：
- `dist <= fixedRopeLength` → 正常移动
- `dist > fixedRopeLength` → **clamp surfaceT**：把候选位置投影到以队友为圆心、绳长为半径的圆上，再反查表面的最近 T 值

这样绳子约束是"预防式"的（移动前 check），而不是"治疗式"的（移动后拉回）。

### 喷气移动可控性
如果需要喷气更可控，方向是：
- 增大 `linearDrag`（减少漂浮感）
- 或在 FixedUpdate 中手动衰减 velocity（当不喷气时）：`rb.velocity *= 0.95f`
- **不要改成 velocity 直设**（会破坏绳子拉力的物理叠加）
