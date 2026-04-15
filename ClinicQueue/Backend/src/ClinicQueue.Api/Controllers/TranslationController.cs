using ClinicQueue.Application.Interfaces;
using ClinicQueue.Infrastructure.ExternalServices.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class TranslationController(ITranslationClient translationClient) : ControllerBase
{
    [HttpPost("detect")]
    public async Task<ActionResult<DetectLanguageResponse>> DetectLanguage([FromBody] DetectLanguageRequest request, CancellationToken cancellationToken)
    {
        var language = await translationClient.DetectAsync(request.Text, cancellationToken);
        return Ok(new DetectLanguageResponse(language));
    }

    [HttpPost]
    public async Task<ActionResult<TranslateTextResponse>> Translate([FromBody] TranslateTextRequest request, CancellationToken cancellationToken)
    {
        var translatedText = await translationClient.TranslateAsync(
            request.Text,
            request.SrcLang,
            request.TargetLang,
            cancellationToken);

        return Ok(new TranslateTextResponse(translatedText));
    }

    [HttpPost("to-english")]
    public async Task<ActionResult<ToEnglishResponse>> ToEnglish([FromBody] ToEnglishRequest request, CancellationToken cancellationToken)
    {
        var (englishText, detectedLang) = await translationClient.ToEnglishAsync(request.Text, cancellationToken);
        return Ok(new ToEnglishResponse(englishText, detectedLang));
    }

    public sealed record DetectLanguageRequest(string Text);
    public sealed record DetectLanguageResponse(string Language);
    public sealed record TranslateTextRequest(string Text, string SrcLang, string TargetLang);
    public sealed record TranslateTextResponse(string TranslatedText);
    public sealed record ToEnglishRequest(string Text);
    public sealed record ToEnglishResponse(string EnglishText, string DetectedLang);
}
