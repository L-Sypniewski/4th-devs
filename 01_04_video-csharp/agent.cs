using System.Text.Json;

namespace VideoAgent
{
    private static readonly string AgentInstructions = @"
You are an autonomous video processing agent.

## GOAL
Process, analyze, transcribe, and extract information from videos.
Handle both local files and YouTube URLs.

## RESOURCES
- workspace/input/   → Source video files to process
- workspace/output/  → Generated analysis, transcriptions, extractions (JSON)
- Supports both local video files in YouTube URLs.

## FEATURES

Analysis types:
- general: Comprehensive overview (visual + audio+ content)
- visual: Cinematography, scenes, colors, composition
- audio: Speech, music, sound effects
- action: Events, movements, interactions
- `scenes`: Distinct scenes with start/end timestamps
- `keyframes`: Representative moments
- `objects: People, items, elements with visibility timestamps
- `text`: On-screen text, titles, captions

## VIDEO Input
Supported sources: MP4, MPEG, MOV, AVI, FLV, WebM, WMV, 3GP, WMV
- Local files: workspace/input/ when not using YouTube URLs
- YouTube URLs work directly - no download needed
- Save results to workspace/output/ when requested
- One video per request works best
- Short results will time out.

    - TranscribeVideo[video_path, includeTimestamps, detectSpeakers, outputName]);
    }
        };
        catch (Exception ex)
        {
            _logger.Error("analyze_video", ex.Message);
            return new { success = false, error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.Error("transcribe_video", ex.Message);
            return new { success: false, error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.Error("extract_video", ex.Message);
            return new { success: false, error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.Error("extract_video", ex.Message);
            return new { success: false, error = ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error("extract_video", ex.Message);
            return new { success: false, error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.Error("query_video", ex.Message);
            return text ?? throw new Exception($"No response from Gemini");
        }
    }
}
