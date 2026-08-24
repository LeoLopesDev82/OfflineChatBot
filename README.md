# 🤖 OfflineChatBot

[![build](https://github.com/LeoLopesDev82/OfflineChatBot/actions/workflows/ci.yml/badge.svg)](https://github.com/LeoLopesDev82/OfflineChatBot/actions/workflows/ci.yml)

A desktop AI chat application built with C# and WPF to demonstrate local inference capabilities using the **Qwen 2.5** model family. The project runs entirely offline without relying on external cloud APIs, ensuring data privacy and local execution.

The core objective of this repository is to showcase software engineering practices, including MVVM architecture, asynchronous programming, thread safety, and integration with C++ bindings for local AI execution via [LLamaSharp](https://github.com/SciSharp/LLamaSharp).

## 🛠️ Technical Highlights

* **Local Inference Engine:** Executes `.gguf` quantized models locally.
* **Vision Support:** Runs multimodal models (LLaVA 1.5 7B) to interpret images attached to the chat, handling the multimodal projection weights and per-turn media state.
* **Integrated Model Manager:** Includes an asynchronous download manager to fetch HuggingFace models directly from the UI, with proper stream handling and progress reporting.
* **Clean Architecture:** Built heavily upon SOLID principles and Single Responsibility, with services behind interfaces and a dependency injection composition root.
* **MVVM Pattern:** Strict separation of UI logic and business rules using `CommunityToolkit.Mvvm`.
* **Resource Management:** Safe handling of unmanaged C++ memory handles (llama.cpp) during model loading, unloading, and deletion.
* **WPF UI:** Features real-time Markdown rendering, syntax highlighting for code blocks, and live CPU/RAM usage indicators.

## 🧱 Project Structure

The solution is split so that everything except the presentation layer is free of any UI framework dependency:

| Project | Target | Responsibility |
| --- | --- | --- |
| `OfflineChatBot.Core` | `net9.0` | Models, service abstractions, local inference, prompt building, model catalog and downloads. No WPF reference. |
| `OfflineChatBot` | `net9.0-windows` | WPF views, view models, behaviors, converters and the platform services that implement the Core abstractions. Composition root lives in `App.xaml.cs`. |
| `OfflineChatBot.Tests` | `net9.0-windows` | xUnit suite covering the parsing, prompt assembly, download state and view model orchestration, using hand written test doubles. |

Run the suite with:

```bash
dotnet test
```

## 🔧 Configuration and Diagnostics

Inference settings live in `appsettings.json`, so the model behaviour can be tuned without recompiling:

```json
{
  "Logging": { "MinimumLevel": "Information" },
  "Generation": {
    "ContextSize": 8192,
    "MaxTokens": 2048,
    "GpuLayerCount": 0,
    "Temperature": 0.7,
    "RepeatPenalty": 1.18,
    "TopK": 40,
    "TopP": 0.95
  }
}
```

Logging goes through `Microsoft.Extensions.Logging` with Serilog behind it, so the Core project depends only on the abstraction. Daily rolling files are written to `%AppData%/OfflineChatBot/Logs/`, keeping the last seven days, and unhandled UI exceptions are recorded before the application goes down.

## 🚀 Getting Started

### Prerequisites
* Windows OS
* .NET 9.0 SDK or later

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/LeoLopesDev82/OfflineChatBot.git
   ```
2. Open the solution in Visual Studio.
3. Build and Run the project.
4. On the first launch, open the **Model Manager** to download a Qwen 2.5 model (e.g., 0.5B or 1.5B) to begin chatting.
5. To analyze images, download **LLaVA 1.5 7B (Vision & Chat)** in the Model Manager, select it as the active model, and attach an image through the composer.

## 🗺️ Roadmap & Future Implementation

The architecture was designed to be extended. Delivered and upcoming milestones:

- [x] **Vision Models:** Support for Vision-Language Models (VLMs), allowing users to attach images to the chat for context-aware interactions.
- [ ] **Document Analysis:** Reading text documents into the conversation, evolving into basic RAG (Retrieval-Augmented Generation).
- [ ] **Hardware Acceleration Options:** Adding explicit UI toggles for CUDA / Vulkan / Metal backends to test inference scaling on dedicated GPUs.

## ⚙️ Technology Stack
* **Language:** C# 13 / .NET 9.0
* **UI Framework:** WPF (Windows Presentation Foundation)
* **Design Pattern:** MVVM
* **AI Engine:** LLamaSharp (llama.cpp wrapper)

## 🤝 Contributing
Contributions, issues, and feature requests are welcome. Feel free to check the issues page.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.
