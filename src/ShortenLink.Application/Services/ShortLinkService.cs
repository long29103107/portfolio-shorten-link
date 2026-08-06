using System.Diagnostics;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Services;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Events;
using ShortenLink.Core.Diagnostics;
using ShortLinkDetailsResponse = ShortenLink.Core.Contracts.Responses.ShortLinkDetailsResponse;

namespace ShortenLink.Application.Services;

public sealed partial class ShortLinkService : IShortLinkService, ITenantAwareShortLinkService
{
    private readonly IShortLinkRepository repository;
    private readonly IShortLinkCache cache;
    private readonly IShortCodeGenerator codeGenerator;
    private readonly TimeProvider timeProvider;
    private readonly int codeLength;
    private readonly int maxCodeGenerationAttempts;
    private readonly IShortLinkEventSink? eventSink;
    private readonly bool diagnosticsEnabled;

    public ShortLinkService(
        IShortLinkRepository repository,
        IShortCodeGenerator codeGenerator,
        IShortLinkCache? cache = null,
        TimeProvider? timeProvider = null,
        int codeLength = Base62ShortCodeGenerator.DefaultCodeLength,
        int maxCodeGenerationAttempts = 10,
        IShortLinkEventSink? eventSink = null,
        bool diagnosticsEnabled = false)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
        this.cache = cache ?? new DisabledShortLinkCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        if (codeLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codeLength), codeLength, "Code length must be greater than zero.");
        }

        if (maxCodeGenerationAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCodeGenerationAttempts), maxCodeGenerationAttempts, "Maximum code generation attempts must be greater than zero.");
        }

        this.codeLength = codeLength;
        this.maxCodeGenerationAttempts = maxCodeGenerationAttempts;
        this.eventSink = eventSink;
        this.diagnosticsEnabled = diagnosticsEnabled;
    }
}
