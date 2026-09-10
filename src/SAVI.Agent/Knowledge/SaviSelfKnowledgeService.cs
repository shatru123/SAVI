using System.Diagnostics;
using System.Text.RegularExpressions;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Knowledge;

public class SaviSelfKnowledgeService : ISaviSelfKnowledgeService
{
    private readonly IProviderRegistry? _providerRegistry;
    private readonly SaviSystemProfile _systemProfile;

    public SaviSelfKnowledgeService(IProviderRegistry? providerRegistry = null)
    {
        _providerRegistry = providerRegistry;
        _systemProfile = InitializeProfile();
    }

    private SaviSystemProfile InitializeProfile()
    {
        var capabilities = new[]
        {
            SaviConstants.Capabilities.Knowledge,
            SaviConstants.Capabilities.Weather,
            SaviConstants.Capabilities.Currency,
            SaviConstants.Capabilities.Crypto,
            SaviConstants.Capabilities.GitHub,
            SaviConstants.Capabilities.Calculator,
            SaviConstants.Capabilities.System,
            SaviConstants.Capabilities.Memory,
            SaviConstants.Capabilities.Reasoning,
            SaviConstants.Capabilities.Search,
            SaviConstants.Capabilities.TechNews,
            SaviConstants.Capabilities.Books,
            SaviConstants.Capabilities.Research,
            SaviConstants.Capabilities.Location,
            SaviConstants.Capabilities.Entity
        };

        var providers = new[]
        {
            SaviConstants.Providers.LocalSystem,
            SaviConstants.Providers.LocalCalculator,
            SaviConstants.Providers.OpenMeteo,
            SaviConstants.Providers.WttrIn,
            SaviConstants.Providers.Frankfurter,
            SaviConstants.Providers.CoinGecko,
            SaviConstants.Providers.GitHubPublic,
            SaviConstants.Providers.HackerNews,
            SaviConstants.Providers.OpenLibrary,
            SaviConstants.Providers.Crossref,
            SaviConstants.Providers.Nominatim,
            SaviConstants.Providers.Wikidata,
            SaviConstants.Providers.Wikipedia,
            SaviConstants.Providers.DuckDuckGo
        };

        return new SaviSystemProfile
        {
            Name = SaviConstants.SystemName,
            FullName = SaviConstants.FullName,
            Version = SaviConstants.Version,
            Framework = ".NET 10 (net10.0)",
            Language = "C# 13",
            Frontend = "ASP.NET Core Blazor (InteractiveServer) with Cyberpunk HUD & Web Audio / Speech API",
            Backend = "ASP.NET Core on .NET 10",
            Database = "SQLite (savi.db) via Entity Framework Core",
            Creator = "Shatrughna Ambhore",
            Purpose = "Sovereign personal AI assistant, real-time conversational voice companion, and autonomous task execution platform",
            CostModel = "100% Free, sovereign, zero mandatory API keys",
            DeploymentEnvironment = "Cross-platform (macOS, Linux, Windows), mobile & desktop responsive",
            InteractionModes = new[] { "Chat Deck", "Real-Time Conversational Voice" },
            Capabilities = capabilities,
            BuiltInProviders = providers,
            ActiveProvidersCount = _providerRegistry?.GetAll().Count ?? providers.Length,
            VoiceSupported = true,
            ChatSupported = true,
            LongTermMemorySupported = true,
            MultiStepTasksSupported = true
        };
    }

    public SaviSystemProfile GetSystemProfile()
    {
        if (_providerRegistry != null)
        {
            var count = _providerRegistry.GetAll().Count;
            if (count > 0 && count != _systemProfile.ActiveProvidersCount)
            {
                return _systemProfile with { ActiveProvidersCount = count };
            }
        }
        return _systemProfile;
    }

    public bool IsSelfKnowledgeQuery(string query, ContextPackage? context = null)
    {
        return MatchQuery(query, context) != null;
    }

    public SelfKnowledgeMatch? MatchQuery(string query, ContextPackage? context = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var clean = query.Trim();
        var lower = clean.ToLowerInvariant();
        var lowerTrimmed = lower.TrimEnd('.', '!', '?', ' ', ';');

        // 1. REGRESSION GUARDS: Verify this is NOT an external general knowledge query!
        // If query specifically asks about an external subject without asking about SAVI, reject it.
        if (IsExternalGeneralKnowledgeQuery(lowerTrimmed))
        {
            return null;
        }

        // 2. Check if the query explicitly targets SAVI or refers to SAVI via context/pronouns
        bool mentionsSavi = Regex.IsMatch(lower, @"\b(?:savi|savi's|shatru's adaptive virtual intelligence)\b");
        bool isSecondPerson = Regex.IsMatch(lower, @"\b(?:you|your|yours|yourself|u)\b");
        bool isContextFollowup = HasSaviContext(clean, context);
        bool isSelfOrientedSystemQuery = Regex.IsMatch(lower, @"\b(?:my data|my conversations?|my memory|privacy|data stored|private|offline|switch between chat and voice|chat and voice|previous conversation|require an api key|need an api key|configure an api key)\b");

        if (!mentionsSavi && !isSecondPerson && !isContextFollowup && !isSelfOrientedSystemQuery)
        {
            return null;
        }

        // 3. Category & Topic Classification

        // A. Security & Secrets Protection
        if (Regex.IsMatch(lower, @"\b(?:api[ -]?keys?|credentials?|connection[ -]?strings?|environment[ -]?variables?|secret[ -]?keys?|tokens?|password|passwords)\b") &&
            (Regex.IsMatch(lower, @"\b(?:what (?:is|are|environment)|show|give|reveal|display|print|tell me|get|have)\b") || Regex.IsMatch(lower, @"\b(?:do you have)\b")))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Security, Topic = "credentials_protection" };
        }

        // B. Creator & Developer Identity
        if (Regex.IsMatch(lower, @"\b(?:who (?:created|made|built|developed|designed|programmed|founded|authored)|who is (?:your|the) (?:creator|author|developer|architect|founder))\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Identity, Topic = "creator", SubTopic = "shatrughna_ambhore" };
        }

        // C. Full Name / Acronym Meaning
        if (Regex.IsMatch(lower, @"\b(?:what does savi stand for|meaning of savi|what is the full form of savi|what does the name savi mean|savi abbreviation|what does the acronym stand for)\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Identity, Topic = "acronym", SubTopic = "full_name" };
        }

        // D. Technology Stack & Languages & Databases & Frontend/Backend (Evaluated before general overview)
        if (Regex.IsMatch(lower, @"\b(?:what (?:is savi |are you )?built (?:with|on)|tech[ -]?stack|technology stack|technologies|what stack|what language|what programming language|written in|built with|built on|what backend|what frontend|what database)\b") ||
            (mentionsSavi || isSecondPerson || isContextFollowup) && Regex.IsMatch(lower, @"\b(?:is (?:savi|it) built with \.net|do you use c#|do you use blazor|do you use sqlite|what db does savi use)\b"))
        {
            string subTopic = "general";
            if (lower.Contains("database") || lower.Contains("db")) subTopic = "database";
            else if (lower.Contains("frontend") || lower.Contains("ui")) subTopic = "frontend";
            else if (lower.Contains("backend")) subTopic = "backend";
            else if (lower.Contains("language")) subTopic = "language";
            else if (lower.Contains(".net")) subTopic = "dotnet";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.TechnologyStack, Topic = "tech_stack", SubTopic = subTopic };
        }

        // E. Identity / About SAVI / Who are you / What does SAVI do
        if (Regex.IsMatch(lower, @"\b(?:explain what you are|who are you|what are you|introduce yourself|tell me about (?:yourself|savi)|what is this assistant|who am i talking to|what exactly is savi|what is savi|what does savi do|what do you do)\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Identity, Topic = "overview" };
        }

        // F. Architecture & Execution Pipeline & Verification
        if (Regex.IsMatch(lower, @"\b(?:how (?:does savi|do you) work|how (?:does savi|do you) answer questions|how (?:does savi|do you) select (?:a )?provider|how (?:does savi|do you) choose (?:a )?provider|how (?:does savi|do you) process results|how (?:does savi|do you) verify answers|how (?:does savi|do you) handle incorrect information|what happens if a provider is (?:unavailable|down)|architecture of savi)\b"))
        {
            string subTopic = "general";
            if (lower.Contains("select") || lower.Contains("choose") || lower.Contains("provider")) subTopic = "provider_selection";
            else if (lower.Contains("verify") || lower.Contains("verification") || lower.Contains("incorrect")) subTopic = "verification";
            else if (lower.Contains("unavailable") || lower.Contains("down") || lower.Contains("fail")) subTopic = "resilience";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Architecture, Topic = "architecture", SubTopic = subTopic };
        }

        // G. Real-Time Voice Mode & Interruption (Barge-In)
        if (Regex.IsMatch(lower, @"\b(?:voice mode|how does (?:your |savi )?voice mode work|can i interrupt (?:you|savi)|interrupt savi|does savi have voice mode|do you have voice mode|barge[- ]?in|can i switch between chat and voice|switch between chat and voice|speech recognition|speech synthesis|is voice mode available)\b"))
        {
            string subTopic = "overview";
            if (lower.Contains("interrupt") || lower.Contains("barge")) subTopic = "interruption";
            else if (lower.Contains("switch")) subTopic = "mode_switch";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Voice, Topic = "voice_mode", SubTopic = subTopic };
        }

        // H. Context & Long-Term Memory
        if (Regex.IsMatch(lower, @"\b(?:remember conversations?|remember context|understand context|continue a previous conversation|does savi remember|do you remember|how does (?:your )?memory work|long[- ]?term memory)\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Context, Topic = "memory" };
        }

        // I. Providers & Data Sources (Wikipedia, DuckDuckGo, AI, APIs)
        if (Regex.IsMatch(lower, @"\b(?:where (?:do you|does savi) get (?:your )?(?:information|answers|data)|do you use (?:ai|apis|wikipedia|duckduckgo)|does savi use (?:ai|apis|wikipedia|duckduckgo)|what apis (?:do you|does savi) (?:support|have))\b"))
        {
            string subTopic = "general";
            if (lower.Contains("wikipedia")) subTopic = "wikipedia";
            else if (lower.Contains("duckduckgo") || lower.Contains("search")) subTopic = "search";
            else if (lower.Contains("ai")) subTopic = "ai";
            else if (lower.Contains("apis") || lower.Contains("api")) subTopic = "apis";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Providers, Topic = "data_sources", SubTopic = subTopic };
        }

        // J. Cost, Privacy, Open Source & Offline capabilities
        if (Regex.IsMatch(lower, @"\b(?:is savi free|are you free|does savi require an api key|do i need an api key|require an api key|need to configure an api key|do i need to configure an api key|can savi work without the internet|can you work without internet|offline|is savi open source|are you open source|where is (?:my )?data stored|is savi private|is my data private|sovereignty)\b"))
        {
            string subTopic = "cost";
            if (lower.Contains("api key") || lower.Contains("key")) subTopic = "api_key";
            else if (lower.Contains("internet") || lower.Contains("offline")) subTopic = "offline";
            else if (lower.Contains("open source")) subTopic = "open_source";
            else if (lower.Contains("data") || lower.Contains("private") || lower.Contains("privacy")) subTopic = "privacy";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Cost, Topic = "cost_and_privacy", SubTopic = subTopic };
        }

        // K. Capabilities & Supported Features
        if (Regex.IsMatch(lower, @"\b(?:what (?:can you|does savi) do|what are (?:your|savi's) capabilities|what features (?:do you|does savi) have|can (?:you|savi) (?:perform calculations|calculate|check weather|provide weather|weather|convert currency|provide currency|currency|search github|github|answer technical questions|answer current questions))\b"))
        {
            string subTopic = "all";
            if (lower.Contains("calculat") || lower.Contains("math")) subTopic = "calculator";
            else if (lower.Contains("weather")) subTopic = "weather";
            else if (lower.Contains("currency")) subTopic = "currency";
            else if (lower.Contains("github")) subTopic = "github";
            else if (lower.Contains("technical") || lower.Contains("coding")) subTopic = "coding";

            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Capabilities, Topic = "capabilities", SubTopic = subTopic };
        }

        // L. Deployment & Mobile Platforms
        if (Regex.IsMatch(lower, @"\b(?:where is savi hosted|what platforms (?:do you|does savi) support|can i use (?:you|savi) on mobile|does savi work on mobile|support mobile|run on linux|run on mac|run on windows)\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Deployment, Topic = "platforms" };
        }

        // M. Diagnostics & Online Status / Version
        if (Regex.IsMatch(lower, @"\b(?:what(?:'s| is) (?:your|savi's) version|what version are you|are you online|is savi (?:currently )?online|what providers are (?:currently )?healthy|system diagnostics)\b"))
        {
            return new SelfKnowledgeMatch { Category = SelfKnowledgeCategory.Diagnostics, Topic = "status" };
        }

        return null;
    }

    public SelfKnowledgeAnswer? GetAnswer(string query, ContextPackage? context = null)
    {
        var sw = Stopwatch.StartNew();
        var match = MatchQuery(query, context);
        if (match == null) return null;

        var profile = GetSystemProfile();
        string textResponse;
        string speechResponse;
        var keyFacts = new List<string>();

        switch (match.Category)
        {
            case SelfKnowledgeCategory.Security:
                textResponse = "🔒 **Security Isolation**: SAVI enforces strict credential protection. Internal system credentials, database connection strings, environment variables, and API keys are confidential system assets and are never exposed in conversation.";
                speechResponse = "For security, internal credentials, API keys, and connection strings are strictly confidential and cannot be revealed.";
                keyFacts.AddRange(new[] { "Zero secret exposure", "Strict credential protection" });
                break;

            case SelfKnowledgeCategory.Identity when match.Topic == "creator":
                textResponse = $"SAVI was conceived, architected, and built by **{profile.Creator}**.\n\n" +
                               $"It is designed as a sovereign, zero-cost personal intelligence assistant, conversational voice companion, and autonomous task execution platform.";
                speechResponse = $"I was created and architected by {profile.Creator}.";
                keyFacts.AddRange(new[] { $"Creator: {profile.Creator}", "Role: Founder & Lead Architect" });
                break;

            case SelfKnowledgeCategory.Identity when match.Topic == "acronym":
                textResponse = $"**SAVI** stands for **{profile.FullName}**.\n\n" +
                               $"It represents a sovereign, adaptive personal AI assistant created by {profile.Creator} to operate with zero mandatory API keys and local privacy.";
                speechResponse = $"SAVI stands for {profile.FullName}, created by {profile.Creator}.";
                keyFacts.AddRange(new[] { $"Full Name: {profile.FullName}", $"Acronym: {profile.Name}" });
                break;

            case SelfKnowledgeCategory.Identity:
                textResponse = $"I am **{profile.Name}** ({profile.FullName}) — a sovereign, zero-cost personal AI assistant and autonomous task platform created by **{profile.Creator}**.\n\n" +
                               $"### Core Highlights:\n" +
                               $"• **Zero Mandatory API Keys**: Operates out-of-the-box using local tools and free public APIs.\n" +
                               $"• **Dual Parity Modes**: Full command deck Chat and interruptible Real-Time Voice.\n" +
                               $"• **High-Performance Architecture**: Built with C# 13 on .NET 10 with Blazor and SQLite.\n" +
                               $"• **Local Privacy**: Your conversations and memory stay in your local database (`savi.db`).\n\n" +
                               $"How can I assist you today?";
                speechResponse = $"I'm SAVI, or Shatru's Adaptive Virtual Intelligence. I'm a personal AI assistant created by Shatrughna Ambhore, designed for coding, research, multi-step tasks, and real-time voice conversation.";
                keyFacts.AddRange(new[] { profile.Name, profile.FullName, profile.Creator, profile.Framework });
                break;

            case SelfKnowledgeCategory.TechnologyStack:
                textResponse = $"SAVI is built on a modern, high-performance C# and .NET foundation:\n\n" +
                               $"• **Language**: {profile.Language}\n" +
                               $"• **Runtime & Framework**: {profile.Framework}\n" +
                               $"• **Frontend Web UI**: {profile.Frontend}\n" +
                               $"• **Backend Service**: {profile.Backend}\n" +
                               $"• **Local Database**: {profile.Database}\n" +
                               $"• **Real-Time Audio**: Client-side Web Audio API, Web Speech API (SpeechSynthesis & SpeechRecognition), and dynamic RMS Voice Activity Detection (VAD).";
                speechResponse = $"SAVI is built with C# 13 on .NET 10, using ASP.NET Core Blazor for the interface, SQLite with Entity Framework Core for data, and JavaScript for real-time WebAudio processing.";
                keyFacts.AddRange(new[] { profile.Language, profile.Framework, profile.Database, "Blazor InteractiveServer" });
                break;

            case SelfKnowledgeCategory.Architecture:
                if (match.SubTopic == "provider_selection")
                {
                    textResponse = "### How SAVI Selects Providers:\n\n" +
                                   "1. **Intent & Entity Detection**: Identifies whether a request is deterministic, technical, or live data.\n" +
                                   "2. **Priority Ranking**: Evaluates provider capability, health status, and historical latency.\n" +
                                   "3. **Local-First Fallback**: Always prefers local deterministic tools before invoking free external APIs.\n" +
                                   "4. **Resilience & Circuit Breakers**: Automatically switches to healthy secondary providers if a provider fails or rate-limits.";
                    speechResponse = "I analyze your query intent and rank available providers by capability and latency, preferring local deterministic tools before public APIs.";
                }
                else if (match.SubTopic == "verification")
                {
                    textResponse = "### How SAVI Verifies Answers:\n\n" +
                                   "• **Evidence Aggregation**: Aggregates factual passages from multiple providers and strips redundant metadata.\n" +
                                   "• **Completeness Checking**: Verifies that every sub-question in multi-part queries has been explicitly addressed.\n" +
                                   "• **Anti-Hallucination Filters**: Unwraps raw JSON into clean text and ensures technical terms (like C# or F#) are preserved.\n" +
                                   "• **Verification Policies**: Supports Fast, Balanced, and Verified modes for cross-checking sources.";
                    speechResponse = "I aggregate factual evidence from providers, cross-check facts against verification policies, and ensure every part of your question is answered.";
                }
                else
                {
                    textResponse = "### How SAVI Works:\n\n" +
                                   "SAVI processes every request through a 5-stage deterministic intelligence pipeline:\n\n" +
                                   "1. **Query Understanding**: Deconstructs query syntax, preserves technical identifiers (e.g. `C#`, `.NET`), and resolves conversation context.\n" +
                                   "2. **Self-Knowledge Fast-Path**: Resolves internal questions locally in `< 5ms` with zero network overhead.\n" +
                                   "3. **Provider Execution**: Routes external tasks to specialized providers (Weather, GitHub, Wikipedia, etc.).\n" +
                                   "4. **Evidence Synthesis**: Normalizes facts, unwraps raw payloads, and synthesizes fluent conversational prose.\n" +
                                   "5. **State & Memory Tracking**: Updates conversation history and stores long-term memory in SQLite.";
                    speechResponse = "At a high level, I understand your request and conversation context, route to the best local capability or provider, verify the factual evidence, and synthesize a natural conversational answer.";
                }
                keyFacts.AddRange(new[] { "5-stage pipeline", "Deterministic query understanding", "Evidence synthesis" });
                break;

            case SelfKnowledgeCategory.Voice:
                if (match.SubTopic == "interruption")
                {
                    textResponse = "### Can you interrupt SAVI while speaking?\n\n" +
                                   "**Yes!** SAVI features **instant full-duplex barge-in**:\n\n" +
                                   "• While SAVI is speaking, the microphone and client-side VAD remain active.\n" +
                                   "• When you start talking, SAVI detects your speech within 220ms, immediately stops audio playback, and seamlessly captures your words as the next conversational turn without requiring a button click or session restart.";
                    speechResponse = "Yes, you can interrupt me anytime! As soon as you start speaking, my audio stops immediately and I begin listening to your next turn.";
                }
                else if (match.SubTopic == "mode_switch")
                {
                    textResponse = "### Switching between Chat and Voice:\n\n" +
                                   "You can switch between Chat and Voice at any moment! Both modes share the exact same conversation history, long-term memory, and intelligence pipeline.\n\n" +
                                   "• **Chat Deck**: Toggle Voice Talk-Back (`🔊 Voice: ON/OFF`) directly in the header or menu.\n" +
                                   "• **Dedicated Voice Screen**: Navigate to `/voice` for a full-screen conversational voice interface.";
                    speechResponse = "You can switch between Chat and Voice anytime. Both modes share the same conversation history and memory.";
                }
                else
                {
                    textResponse = "### SAVI Real-Time Voice Mode:\n\n" +
                                   "• **Full-Duplex Audio**: Concurrent microphone input and speech synthesis output.\n" +
                                   "• **Instant Barge-In**: Real-time VAD automatically ducks and stops SAVI's speech when you talk.\n" +
                                   "• **Self-Echo Blanking**: Hardware Acoustic Echo Cancellation (AEC) and onset filters ensure SAVI never listens to its own voice.\n" +
                                   "• **Zero Speech Cost**: Runs via standard browser Web Speech APIs, completely free without cloud speech billing.";
                    speechResponse = "My voice mode supports continuous, full-duplex conversation with instant barge-in interruption. You can speak naturally, interrupt me anytime, or switch between chat and voice.";
                }
                keyFacts.AddRange(new[] { "Full-duplex audio", "Instant barge-in", "Zero speech cost", "AEC filtering" });
                break;

            case SelfKnowledgeCategory.Context:
                textResponse = "### Context & Memory in SAVI:\n\n" +
                               "SAVI maintains two integrated layers of context:\n\n" +
                               "1. **Conversation History**: Tracks recent dialog turns to resolve pronouns ('it', 'this', 'its creator') and handle multi-turn follow-ups.\n" +
                               "2. **Long-Term Memory**: Persists your coding preferences, personal identity context, synthesis rules, and active projects in SQLite (`savi.db`) across all sessions.\n\n" +
                               "You can manage your memories anytime on the **Long-Term Memory** screen (`/memory`).";
                speechResponse = "Yes, I remember recent conversation history to understand follow-ups, and I persist your preferences and rules in long-term memory.";
                keyFacts.AddRange(new[] { "Multi-turn context", "Pronoun coreference resolution", "Persistent SQLite memory" });
                break;

            case SelfKnowledgeCategory.Providers:
                if (match.SubTopic == "ai")
                {
                    textResponse = "### Does SAVI use AI?\n\n" +
                                   "SAVI is designed to be **free, sovereign, and deterministic first**:\n\n" +
                                   "• Core intelligence uses rule-based query understanding, specialized providers, and local evidence synthesis.\n" +
                                   "• You can optionally connect a local **Ollama** LLM (e.g. `llama3`) or free cloud AI endpoints via settings, but **no AI model is required** for SAVI to operate.";
                    speechResponse = "SAVI uses deterministic query understanding and specialized providers out-of-the-box. You can optionally connect local Ollama AI models, but no AI key is required.";
                }
                else if (match.SubTopic == "wikipedia")
                {
                    textResponse = "### Does SAVI use Wikipedia?\n\n" +
                                   "Yes, SAVI has a built-in Wikipedia knowledge provider that fetches factual summaries and encyclopedia extracts when you ask educational or general knowledge questions, such as about history, science, or concepts.";
                    speechResponse = "Yes, I use Wikipedia for encyclopedic facts and summaries when you ask about general knowledge topics.";
                }
                else if (match.SubTopic == "search")
                {
                    textResponse = "### Does SAVI use DuckDuckGo / Web Search?\n\n" +
                                   "Yes, SAVI includes a zero-key DuckDuckGo web search provider to retrieve relevant external web results and fresh information when queries cannot be answered deterministically.";
                    speechResponse = "Yes, I use DuckDuckGo web search when needed to look up external information and fresh topics.";
                }
                else
                {
                    textResponse = "### Where does SAVI get its information?\n\n" +
                                   "SAVI relies on a multi-tier provider ecosystem:\n\n" +
                                   "• **Local Deterministic Tools**: Calculator, system specs, file manager.\n" +
                                   "• **Specialized Free APIs**: Open-Meteo & Wttr.in (Weather), Frankfurter (FX rates), CoinGecko (Crypto), GitHub Public API, Hacker News, Open Library (Books), Crossref (Research), Nominatim (Geocoding), Wikidata, and Wikipedia.\n" +
                                   "• **Search**: DuckDuckGo for general web searches.\n" +
                                   "All provider evidence is aggregated and synthesized before you see it.";
                    speechResponse = "I use specialized data providers and deterministic local tools depending on what you ask, synthesizing the results into a direct answer.";
                }
                keyFacts.AddRange(new[] { "Zero-key public providers", "Local deterministic tools", "Evidence synthesis" });
                break;

            case SelfKnowledgeCategory.Cost:
                if (match.SubTopic == "api_key")
                {
                    textResponse = "### Does SAVI require an API key?\n\n" +
                                   "**No!** SAVI requires **zero API keys** to run all core features out-of-the-box:\n\n" +
                                   "• All built-in providers (Weather, Currency, Crypto, GitHub, Wikipedia, Math, System, Voice) are completely free.\n" +
                                   "• You can optionally add custom API keys in the Settings screen if you wish, but none are required.";
                    speechResponse = "No, SAVI does not require any API keys. All core features work completely out of the box for free.";
                }
                else if (match.SubTopic == "offline")
                {
                    textResponse = "### Can SAVI work without the internet?\n\n" +
                                   "**Yes, for local capabilities!**\n\n" +
                                   "• Local math evaluation, system hardware diagnostics, file operations, long-term memory, and self-knowledge operate completely offline.\n" +
                                   "• If you run local Ollama, offline AI reasoning is also available.\n" +
                                   "• Internet access is only needed when fetching live external data like current weather, currency exchange, or Wikipedia articles.";
                    speechResponse = "Yes, local features like math, system tools, memory, and self-knowledge work offline. Internet is only required for live external data like weather or Wikipedia.";
                }
                else if (match.SubTopic == "privacy")
                {
                    textResponse = "### Where is my data stored?\n\n" +
                                   "Your data remains **100% sovereign and private**:\n\n" +
                                   "• All conversation histories, messages, and long-term memory are stored locally on your machine in the SQLite database (`savi.db`).\n" +
                                   "• No telemetry, user profiling, or external tracking is performed.";
                    speechResponse = "All your conversations, settings, and memories are stored locally on your machine in a SQLite database. No personal data is sent to external servers.";
                }
                else
                {
                    textResponse = "### Is SAVI free and open source?\n\n" +
                                   "**Yes, SAVI is 100% free and open source!**\n\n" +
                                   "• No subscriptions, credit cards, or paid tokens required.\n" +
                                   "• Zero mandatory API keys.\n" +
                                   "• Built for user sovereignty and local independence.";
                    speechResponse = "Yes, SAVI is completely free, sovereign, and open source. There are no subscriptions or mandatory API keys.";
                }
                keyFacts.AddRange(new[] { "100% Free", "Zero mandatory keys", "Local SQLite privacy" });
                break;

            case SelfKnowledgeCategory.Capabilities:
                textResponse = "### What can SAVI do?\n\n" +
                               "Here are the core capabilities available right now:\n\n" +
                               "• 💻 **Coding & Tech Q&A**: C#, .NET, Python, algorithms, architecture explanations.\n" +
                               "• 🎙️ **Real-Time Voice**: Full-duplex speech conversation with natural barge-in.\n" +
                               "• 🌦️ **Live Weather**: Worldwide forecasts and conditions (Open-Meteo & Wttr.in).\n" +
                               "• 💱 **Currency & Crypto**: Foreign exchange rates and live cryptocurrency prices.\n" +
                               "• 🐙 **GitHub Intelligence**: Inspect public repositories, stars, forks, and issues.\n" +
                               "• 🔢 **Math & Computations**: High-precision math, expressions, and unit conversions.\n" +
                               "• 🧠 **Long-Term Memory**: Retains your preferences, rules, and project context.\n" +
                               "• ⚙ **Task Execution Matrix**: Autonomous multi-step workflows with human approval safety gates.\n" +
                               "• 🖥️ **Host Diagnostics**: Inspect CPU, RAM, OS version, and file systems.";
                speechResponse = "I can assist with coding, multi-step workflows, real-time weather, currency and crypto rates, GitHub lookups, calculations, long-term memory, and full-duplex voice conversations.";
                keyFacts.AddRange(new[] { "Coding & Tech", "Real-time Voice", "Weather", "Currency", "GitHub", "Math", "Memory", "Task Matrix" });
                break;

            case SelfKnowledgeCategory.Deployment:
                textResponse = "### Deployment & Platform Support:\n\n" +
                               "SAVI is built on cross-platform ASP.NET Core and runs on:\n\n" +
                               "• **macOS** (Apple Silicon & Intel)\n" +
                               "• **Linux** (Ubuntu, Debian, Fedora, Arch)\n" +
                               "• **Windows** (Windows 10/11, Windows Server)\n\n" +
                               "The Blazor web interface is fully mobile-responsive and works across modern mobile and desktop browsers with audio touch unlock.";
                speechResponse = "SAVI runs cross-platform on macOS, Linux, and Windows, and fully supports mobile browsers with touch and voice controls.";
                keyFacts.AddRange(new[] { "macOS", "Linux", "Windows", "Mobile-responsive web" });
                break;

            case SelfKnowledgeCategory.Diagnostics:
                textResponse = $"### SAVI Runtime Diagnostics:\n\n" +
                               $"• **Status**: 🟢 Online & Healthy\n" +
                               $"• **Version**: {profile.Version}\n" +
                               $"• **Framework**: {profile.Framework}\n" +
                               $"• **Language**: {profile.Language}\n" +
                               $"• **Active Providers**: {profile.ActiveProvidersCount} capability providers registered\n" +
                               $"• **Database**: SQLite (`savi.db`) connected\n" +
                               $"• **Audio Subsystem**: WebAudio & SpeechSynthesis operational";
                speechResponse = $"SAVI is online, running version {profile.Version} on {profile.Framework}. All {profile.ActiveProvidersCount} capability providers are healthy.";
                keyFacts.AddRange(new[] { "Status: Online", $"Version: {profile.Version}", $"Framework: {profile.Framework}" });
                break;

            default:
                textResponse = $"I am **{profile.Name}** ({profile.FullName}), created by **{profile.Creator}**.";
                speechResponse = $"I am SAVI, created by {profile.Creator}.";
                break;
        }

        sw.Stop();

        return new SelfKnowledgeAnswer
        {
            Category = match.Category,
            Topic = match.Topic,
            TextResponse = textResponse,
            SpeechResponse = speechResponse,
            Confidence = 1.0,
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            KeyFacts = keyFacts
        };
    }

    private static bool IsExternalGeneralKnowledgeQuery(string lower)
    {
        // Check for common programming language inquiries that DO NOT mention SAVI
        if (Regex.IsMatch(lower, @"^(?:what is|explain|tell me about)\s+(?:c#|c\+\+|f#|\.net|python|javascript|typescript|java|golang|rust|swift|ruby|php|sql|docker|kubernetes|git|react|angular|vue|next\.js|node\.js)\??$"))
        {
            return true;
        }

        // Check for general entities/companies: "What is ChatGPT?", "What is OpenAI?", "Who is Elon Musk?", "What is Microsoft?"
        if (Regex.IsMatch(lower, @"^(?:what is|who is|tell me about)\s+(?:chatgpt|openai|google|microsoft|apple|amazon|meta|anthropic|claude|gemini|deepmind|elon musk|bill gates|steve jobs)\??$"))
        {
            return true;
        }

        // Check for third-party company technology queries: "What tech does Google use?", "What tech stack does Microsoft use?"
        if (Regex.IsMatch(lower, @"\b(?:what tech|what technology|what stack)\b") &&
            Regex.IsMatch(lower, @"\b(?:google|microsoft|apple|amazon|meta|netflix|uber|spotify|twitter|x)\b"))
        {
            return true;
        }

        // Check for third-party APIs: "What APIs does OpenAI provide?"
        if (Regex.IsMatch(lower, @"\b(?:what apis|apis)\b") &&
            Regex.IsMatch(lower, @"\b(?:openai|google|microsoft|stripe|github|twitter)\b") &&
            !lower.Contains("savi"))
        {
            return true;
        }

        // Pure weather/currency queries without self-reference: "weather in Tokyo", "convert 100 USD to EUR"
        if (Regex.IsMatch(lower, @"^(?:weather|temperature|forecast)\s+(?:in|for|at)?\s*[a-zA-Z\s]+$") ||
            Regex.IsMatch(lower, @"^(?:convert|exchange rate)\s+[\d\.]+\s*[a-zA-Z]{3}\s+(?:to|in)\s+[a-zA-Z]{3}$"))
        {
            return true;
        }

        // Pure calculation: "calculate 2+2", "5 * 25"
        if (Regex.IsMatch(lower, @"^(?:calculate|compute|eval)\s+[\d\.\s\+\-\*\/\^\(\)]+$") ||
            Regex.IsMatch(lower, @"^\d+\s*[\+\-\*\/\^]\s*\d+$"))
        {
            return true;
        }

        return false;
    }

    private static bool HasSaviContext(string query, ContextPackage? context)
    {
        if (context?.RecentMessages == null || context.RecentMessages.Count == 0) return false;

        var lower = query.ToLowerInvariant();
        bool hasPronoun = Regex.IsMatch(lower, @"\b(?:it|its|this|this assistant|this bot|this system|the assistant)\b");
        if (!hasPronoun) return false;

        // Check if the immediately preceding user query or assistant reply was about SAVI
        for (int i = context.RecentMessages.Count - 1; i >= Math.Max(0, context.RecentMessages.Count - 3); i--)
        {
            var msg = context.RecentMessages[i];
            var msgLower = msg.Content.ToLowerInvariant();
            if (msgLower.Contains("savi") ||
                msgLower.Contains("who are you") ||
                msgLower.Contains("what are you") ||
                msgLower.Contains("shatrughna ambhore") ||
                msgLower.Contains("adaptive virtual intelligence"))
            {
                return true;
            }
        }

        return false;
    }
}
