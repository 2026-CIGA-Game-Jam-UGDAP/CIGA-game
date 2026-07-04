# 48h GameJam

当下我们在 48 小时 Game Jam 场景下开发游戏，技术栈为：Unity + C#（本地多人）。这个时间限制和技术栈对我们的设计决策有很大影响。

## 游戏类型

2D 本地多人（同屏）

## 基本信息

Unity 版本：2022.3.62f3
现有插件：Cinemachine 2.10.7, TextMeshPro、DotWeen、Obi Rope、Cinemachine、TextAnimation Pro 等插件,对于部分 UI 动画或字体动画请用插件自带的组件实现，避免重复造轮子。
渲染管线：URP
输入方案：旧 input system

## 可接受的代码风格

工程化，GameJam 风格，少抽象，少写接口，直调用