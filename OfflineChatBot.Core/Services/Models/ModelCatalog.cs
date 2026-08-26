using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Models
{
    public static class ModelCatalog
    {
        public static List<ModelInfo> CreatePresets()
        {
            return new List<ModelInfo>
            {
                new ModelInfo
                {
                    Name = "Qwen 2.5 0.5B Instruct (Ultra Light)",
                    FileName = "qwen2.5-0.5b-instruct-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/qwen2.5-0.5b-instruct-q4_k_m.gguf",
                    SizeInMB = 397,
                    Description = "Ultra-lightweight 500M parameter model. Ideal for quick tests and low-memory machines. Fast but limited reasoning."
                },
                new ModelInfo
                {
                    Name = "Qwen 2.5 Coder 1.5B (Recommended)",
                    FileName = "qwen2.5-coder-1.5b-instruct-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-Coder-1.5B-Instruct-GGUF/resolve/main/qwen2.5-coder-1.5b-instruct-q4_k_m.gguf",
                    SizeInMB = 1060,
                    Description = "Best balance of speed and intelligence. Excellent for coding assistance, general conversation, and creative writing."
                },
                new ModelInfo
                {
                    Name = "Qwen 2.5 3B Instruct (High Intelligence)",
                    FileName = "qwen2.5-3b-instruct-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf",
                    SizeInMB = 1930,
                    Description = "Highly articulated 3B parameter model. Natural conversational flow with advanced reasoning and multilingual support."
                },
                new ModelInfo
                {
                    Name = "Qwen 2.5 Coder 3B Instruct (Advanced Coding)",
                    FileName = "qwen2.5-coder-3b-instruct-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-Coder-3B-Instruct-GGUF/resolve/main/qwen2.5-coder-3b-instruct-q4_k_m.gguf",
                    SizeInMB = 1930,
                    Description = "Dedicated coding model with deep understanding of programming languages, algorithms, and software architecture."
                },
                new ModelInfo
                {
                    Name = "Qwen 2.5 7B Instruct (Maximum Intelligence)",
                    FileName = "qwen2.5-7b-instruct-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-7B-Instruct-GGUF/resolve/main/qwen2.5-7b-instruct-q4_k_m.gguf",
                    SizeInMB = 4680,
                    Description = "Most powerful model in the lineup. Near GPT-level intelligence for complex analysis, long-form writing, and expert-level coding."
                },
                new ModelInfo
                {
                    Name = "LLaVA 1.5 7B (Vision & Chat)",
                    FileName = "llava-v1.5-7b-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/second-state/Llava-v1.5-7B-GGUF/resolve/main/llava-v1.5-7b-Q4_K_M.gguf",
                    MmprojFileName = "llava-v1.5-7b-mmproj-model-f16.gguf",
                    MmprojDownloadUrl = "https://huggingface.co/second-state/Llava-v1.5-7B-GGUF/resolve/main/llava-v1.5-7b-mmproj-model-f16.gguf",
                    IsVisionModel = true,
                    SizeInMB = 4700,
                    Description = "Vision model capable of interpreting images. Supports uploading images into the chat."
                }
            };
        }
    }
}