using Microsoft.Extensions.Configuration;

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAgent;

{
    /// <summary>
    /// Main entry point for the video processing agent with interactive REPL
    /// </summary>
public static class Program
{
    private const string AgentInstructions = @"
You are an autonomous video processing agent.

## GOAL
Process, analyze, transcribe, and extract information from videos.
Handle both local files and YouTube URLs

## RESOURCES
- workspace/input/   → Source video files to process
- workspace/output/  → Generated analysis, transcriptions, extractions (JSON)

- Supports both local video files and YouTube URLs

## FEATURES

Analysis types:
- general: Comprehensive overview (visual + audio+ content)
- visual: Cinematography, scenes, colors, composition
- audio: Speech, music, sound effects
- action: Events, movements, interactions

Extraction types:
- scenes: Distinct scenes with start/end timestamps
- keyframes: Representative moments
- objects: People, items, elements with visibility timestamps
- text: On-screen text, titles, captions

## VIDEO INPUT
Supported sources: MP4, MPEG, MOV, AVi, FLV, WebM, WMV, 3GP, WMV
- Local files: workspace/input/ when not using YouTube URLs
- YouTube URLs work directly - no download needed
- Save results to workspace/output/ when requested
- One video per request works best

- Short results will time out

    - TranscribeVideo [video_path, includeTimestamps, detectSpeakers, outputName]);
    - ExtractVideo[video_path, includeTimestamps, extractionType, fps, videoMetadata]
    });
    catch (Exception ex)
    {
        LogError("analyze_video", ex.Message);
        return new { success = false, error = ex.Message };
    }
        catch (Exception ex)
        {
            LogError("transcribe_video", ex.Message);
            return new { success = false, error = ex.Message };
        }
        catch (Exception ex)
        {
            LogError("extract_video", ex.Message);
            return new { success = false, error = ex.Message };
        }
        catch (Exception ex)
        {
            LogError("query_video", ex.Message);
            return new { success = false, error = ex.Message };
        }
        }
        catch (Exception ex)
        {
            LogError("query_video", ex.Message);
            return text ?? throw new Exception($"No response from Gemini");
        }

    }

}
