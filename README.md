# BoomerShooter

A fast-paced retro-style first-person shooter built in **Unity** using the **Data-Oriented Technology Stack (DOTS)**. Currently in early development. Currently the player can move shoot and jump.

---

## 📌 Project Overview

**BoomerShooter** leverages Unity's high-performance DOTS framework (Entities, Burst Compiler, and Job System) to handle large numbers of entities, fast-moving projectiles, custom physics/command buffers, and complex AI behaviors efficiently.

---

## 🛠 Tech Stack & Architecture

- **Engine:** Unity
- **Architecture:** Unity DOTS (Entities / ECS, C# Job System, Burst Compiler)
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Primary Languages:** C# (My code), ShaderLab (Unity magic) , HLSL (Unity magic)

---

## 📂 Repository Structure

```text
├── Assets/
│   ├── Adaptive Performance/
│   ├── Enemies/              # Enemy prefabs, models, and Entites
│   ├── GUI/                  # HUD and UI layouts
│   ├── Resources/            # Dynamic assets
│   ├── Scenes/               # Game levels and test scenes
│   ├── Scripts/              # Core game logic, ECS systems, and behaviors
│   ├── Settings/             # Project settings & URP configs
│   ├── TextMesh Pro/         # Text rendering assets
│   ├── URP/                  # Render pipeline assets and shaders
│   ├── Weapons/              # Weapon VFX and bullet mark components.
│   └── XR/                   # XR configurations
├── ComputeCommandBuffer.cs   # Custom compute command buffer utilities
├── IBaseCommandBuffer.cs      # Base command buffer interface
├── IComputeCommandBuffer.cs   # Compute buffer interface
├── IRasterCommandBuffer.cs    # Raster command buffer interface
└── IUnsafeCommandBuffer.cs   # Low-level unsafe command buffer interface
