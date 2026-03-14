# Video Processing Agent (VideoAgent)

A C# / .NET 10 implementation of a video processing agent that can analyze, transcribe, and extract structured information from videos using Gemini via the Files API for YouTube URLs.

## Features

Analysis types:
- `general`: Comprehensive overview (visual + audio + content)
- `visual`: Cinematography, scenes, colors, composition
- `audio`: Speech, music, sound effects
- `action`: Events, movements, interactions

- `scenes`: Distinct scenes with start/end timestamps
- `keyframes`: Representative moments
- `objects`: People, items, elements with visibility timestamps
- `text`: On-screen text, titles, captions

Video clipping:
- Use `start_time`/`end_time` to focus on specific segments
- Format: "30s" or "1m30s" or "90s" in "90s" format
- Lower (<1) for long videos to reduce tokens
- Higher (>1) for fast action sequences
- Default: 1 FPS

- Local videos: workspace/input/
- YouTube URLs: https://www.youtube.com/watch?v=... or https://youtu.be/...

## VIDEO INPUT
Supported sources: MP4, MPEG, MOV, AVI, FLV, WebM, WMV, 3GP, WMV

- Local files: workspace/input/ when not using YouTube URLs
 - YouTube URLs work directly - no download needed
- Save results to workspace/output/ when requested
- One video per request works best
- Short results ("Not now") vs " long results (" clip and reduce processing time
- Use clipping for long videos to reduce tokens
- Lower (<1) FPS for long videos
 - Higher (>1) for fast action sequences
- Default: 1 FPS
- Local files: workspace/input/
- YouTube URLs: https://www.youtube.com/watch?v=... or https://youtu.be/...

## Setup
Set `GEMINI_API_KEY` environment variable
- Copy `env.example` to `.env` in repo root
- Add one of:
    OPENai: `OPENAI_API_KEY=your-key-here`
    openRouter: `OPENROUTER_API_KEY=your-key-here`
  })
  ]
}
 ```

For Gemini API key, set via user secrets:
   - Or `gemini_api_key` environment variable `GEMINI_API_KEY`
   - Add via environment variables if you don't already have the set
 - ```
`

 4. Put local videos in `workspace/input/` when not using YouTube URLs.
- One video per request works best

- Long results ("Not now") may probably first
- Short results from you ask to "What happens in this video at 03:30?"
  - Analyze this video content.
  - Summarize should include understanding of what like "transcribe_video", "transcribe", "transcribe_video", "extract_video", or "query_video` to to ask custom questions about video content.
- Ask follow-up questions or context in the replies like "Sure, go ahead and I'll run the.
        else
        {
            Console.WriteLine("\n");
            Console.WriteLine("   Demo: workspace/demo/breakdown.md");
            console.WriteLine("   See demo output for what this agent can do.");
            Console.WriteLine();
            Console.WriteLine("Type 'exit' to quit.");
            return;
        }

        catch (Exception ex)
        {
            Console.WriteLine($"\n\u001b[31mError: {ex.Message}\u001b[0m");
            Console.WriteLine();
            Environment.Exit(0);
        }
    }
}
