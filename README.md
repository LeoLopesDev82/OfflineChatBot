# 🤖 OfflineChatBot

[![build](https://github.com/LeoLopesDev82/OfflineChatBot/actions/workflows/ci.yml/badge.svg)](https://github.com/LeoLopesDev82/OfflineChatBot/actions/workflows/ci.yml)

A desktop AI chat application built with C# and WPF to demonstrate local inference capabilities using the **Qwen 2.5** model family. The project runs entirely offline without relying on external cloud APIs, ensuring data privacy and local execution.

The core objective of this repository is to showcase software engineering practices, including MVVM architecture, asynchronous programming, thread safety, and integration with C++ bindings for local AI execution via [LLamaSharp](https://github.com/SciSharp/LLamaSharp).

## 🛠️ Technical Highlights

* **Local Inference Engine:** Executes `.gguf` quantized models locally.
* **GPU Acceleration:** Offloads model layers to the GPU through the Vulkan backend, falling back to the CPU automatically when no compatible device is available or when the model does not fit in video memory.
* **Document Question Answering:** Reads PDF, Word and text files, splits them into passages, turns each one into a vector with a dedicated embedding model and retrieves only the passages that match the question, so a document far larger than the context window can still be discussed.
* **Context Budgeting:** Counts real tokens with the model tokenizer and trims the oldest turns to fit the context window, reserving room for the answer instead of guessing with a fixed message count.
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
    "MaxHistoryTokens": 2048,
    "UseGpu": true,
    "GpuLayerCount": 99,
    "Temperature": 0.7,
    "RepeatPenalty": 1.18,
    "TopK": 40,
    "TopP": 0.95
  }
}
```

`UseGpu` can also be toggled at runtime from the Model Manager, which reloads the active model and reports the device in the status bar. Vulkan was chosen over CUDA because it only requires an up to date graphics driver, works on NVIDIA, AMD and Intel hardware, and keeps the restore under 100 MB instead of the 1.4 GB the CUDA backend pulls in.

Measured on a GeForce GTX 1050 Ti (4 GB) with Qwen 2.5 3B Q4_K_M, all 37 layers offloaded:

| | CPU | GPU (Vulkan) |
| --- | --- | --- |
| Model load | 8.0s | 3.5s |
| Time to first token | 2.0s | 0.3s |
| Generation | 8.1 tok/s | 29.6 tok/s |

The status bar reports live GPU utilization and dedicated video memory, read from the same Windows performance counters that Task Manager uses, alongside the device actually running the model, how many layers were offloaded and the measured generation speed. Both numbers are needed to read the situation correctly: the application always draws its own interface on the GPU, so a few percent of activity there says nothing about where inference is running.

**Model size decides whether the GPU is used at all.** The weights must fit in video memory, so on a 4 GB card the 3B model loads entirely on the GPU while the 7B vision model does not fit and falls back to the CPU. That fallback is expected rather than a failure, and the status bar shows `Device: CPU` when it happens. Partial offloading is possible by lowering `GpuLayerCount`, but it was measured at 16 of 33 layers and produced only 5.3 tok/s against 4.8 tok/s on the CPU, so it is rarely worth it. Be aware that intermediate values can exhaust video memory during allocation and terminate the process, since that failure happens inside the native library and cannot be caught; leaving the setting at 99 keeps the safe path, where a load failure is handled and falls back to the CPU.

Logging goes through `Microsoft.Extensions.Logging` with Serilog behind it, so the Core project depends only on the abstraction. Daily rolling files are written to `%AppData%/OfflineChatBot/Logs/`, keeping the last seven days, and unhandled UI exceptions are recorded before the application goes down.

## 📄 Talking to a Document

Attaching a file does not push the whole document into the prompt, which would never fit. The text is extracted (PdfPig for PDF, OpenXml for Word), split into passages of about three hundred and fifty tokens with a small overlap, and each passage is turned into a vector by a dedicated embedding model. When a question arrives it becomes a vector too, and only the closest passages travel with it to the chat model.

The embedding model is a separate download in the Model Manager and is never selectable as a conversation model: it produces vectors, not answers. Attaching a document is blocked until it is present, and a chat model is still required to write the reply. Vectors are stored next to the conversation and erased with it, so reopening the application does not pay the indexing cost again.

Passage size and model choice were measured rather than assumed, and the measurements did not always point where intuition did. Indexing cost is proportional to the amount of text, not to the number of passages, so larger passages buy no speed at all while they dilute the sentence that answers the question among unrelated paragraphs: the same document indexed at seven hundred tokens per passage lost an answer that three hundred and fifty tokens retrieved correctly. A hundred megabyte embedding model indexed that document in 1.2s against 2.9s for the three hundred megabyte one and scored the same on the retrieval benchmark, yet the answers it produced on real documents were visibly worse, so the larger model stayed. Retrieval benchmarks are a proxy for answer quality, not a substitute for reading the answers.

Indexing is the slow step, and it reports itself. The file appears in the composer the moment it is picked, marked as attaching, and sending stays disabled until the passages are ready, so the wait reads as work in progress rather than as a frozen window.

Retrieval answers questions, it does not summarise. Asking what a clause says works because the answer lives in one passage; asking to summarise an entire book does not, because only a handful of passages ever reach the model. That is a property of the technique rather than a defect, and whole document summarisation needs a hierarchical pass that is not implemented here.

Scanned PDFs have no text layer and are rejected with an explanation, since character recognition is not supported. The legacy binary `.doc` format and spreadsheets are out of scope: tabular data needs aggregation or querying rather than similarity search.

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
6. To ask questions about a document, download **EmbeddingGemma 300M (Document Search)** in the Model Manager, then attach a PDF, Word or text file through the composer. It is indexed once and stays available for that conversation.

## 🗺️ Roadmap & Future Implementation

The architecture was designed to be extended. Delivered and upcoming milestones:

- [x] **Vision Models:** Support for Vision-Language Models (VLMs), allowing users to attach images to the chat for context-aware interactions.
- [x] **Document Analysis:** Retrieval augmented generation over PDF, Word and text files, with a local embedding model and persisted vectors.
- [ ] **Spreadsheet Analysis:** Answering questions over tabular data through aggregation and querying instead of similarity search.
- [x] **Hardware Acceleration:** GPU offloading through the Vulkan backend, toggleable at runtime, with automatic fallback to the CPU.

## ⚙️ Technology Stack
* **Language:** C# 13 / .NET 9.0
* **UI Framework:** WPF (Windows Presentation Foundation)
* **Design Pattern:** MVVM
* **AI Engine:** LLamaSharp (llama.cpp wrapper)

## 🤝 Contributing
Contributions, issues, and feature requests are welcome. Feel free to check the issues page.

## 📝 License
This project is licensed under the MIT License - see the LICENSE file for details.
