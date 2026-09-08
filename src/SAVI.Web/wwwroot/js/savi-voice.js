// SAVI Voice Engine — Full-Duplex Real-Time Voice Conversation Architecture
// Decoupled AudioCaptureController & AudioPlaybackController
// Off-Thread AudioWorkletProcessor with ScriptProcessor Fallback
// 300ms Pre-Roll & 150ms Post-Roll Ring Buffer, Adaptive VAD with Hysteresis, and Single Authoritative Barge-In

class UserTurnContext {
    constructor() {
        this.turnId = 'turn_' + Math.random().toString(36).substring(2, 9) + '_' + Date.now();
        this.partialTranscript = '';
        this.finalTranscript = '';
        this.startTime = performance.now();
        this.endTime = null;
        this.hasDispatched = false;
        this.isInterrupted = false;
        this.hasPreRollAudio = false;
    }

    getFullText() {
        return (this.finalTranscript + ' ' + this.partialTranscript).trim();
    }
}

class AudioPlaybackController {
    constructor(saviVoice) {
        this.saviVoice = saviVoice;
        this.isSpeaking = false;
        this.isChunkSpeaking = false;
        this.chunkQueue = [];
        this.currentUtterance = null;
        this.availableVoices = [];
        this.currentTtsTurnId = null;
        this.currentSpokenText = '';
        this.allCurrentText = '';
        this.lastSpokenTimestamp = 0;
        this.lastStopLatencyMs = 0;
        this.recentSpokenSentences = []; // Rolling buffer of recent chunks with timestamps
        this.resumeHeartbeat = null;
        this.pendingSpeakTimeout = null;

        this.loadVoices();
        if (typeof window !== 'undefined' && window.speechSynthesis) {
            window.speechSynthesis.onvoiceschanged = () => this.loadVoices();
        }
    }

    loadVoices() {
        if (typeof window !== 'undefined' && window.speechSynthesis) {
            const voices = window.speechSynthesis.getVoices();
            if (voices && voices.length > 0) {
                this.availableVoices = voices;
            }
        }
    }

    ensureSpeechActive() {
        if (typeof window !== 'undefined' && window.speechSynthesis) {
            try {
                if (window.speechSynthesis.paused) {
                    window.speechSynthesis.resume();
                }
            } catch (_) {}
        }
    }

    startResumeWatchdog() {
        if (!this.resumeHeartbeat) {
            this.resumeHeartbeat = setInterval(() => {
                if (this.isSpeaking && typeof window !== 'undefined' && window.speechSynthesis) {
                    try {
                        if (window.speechSynthesis.paused) {
                            window.speechSynthesis.resume();
                        }
                    } catch (_) {}
                } else if (!this.isSpeaking) {
                    this.stopResumeWatchdog();
                }
            }, 2000);
        }
    }

    stopResumeWatchdog() {
        if (this.resumeHeartbeat) {
            clearInterval(this.resumeHeartbeat);
            this.resumeHeartbeat = null;
        }
    }

    speak(text, rate = 1.0, turnId = null) {
        if (typeof window === 'undefined' || !window.speechSynthesis) return;

        try {
            if (this.pendingSpeakTimeout) {
                clearTimeout(this.pendingSpeakTimeout);
                this.pendingSpeakTimeout = null;
            }

            // Duck and halt any prior speech immediately
            const wasSpeaking = this.isSpeaking || window.speechSynthesis.speaking;
            if (wasSpeaking) {
                this.duckAndStop(false);
            }

            this.ensureSpeechActive();

            // Strip code blocks, markdown tables, and markdown formatting for natural voice output
            let cleanText = text.replace(/```[\s\S]*?```/g, ' The code solution is displayed on screen. ')
                .replace(/\|[^\n]+\|\r?\n\|[-:\s|]+\|\r?\n(?:\|[^\n]+\|\r?\n?)*/g, ' The structured data is shown on your screen. ')
                .replace(/\*\*(.*?)\*\*/g, '$1')
                .replace(/\*(.*?)\*/g, '$1')
                .replace(/`{1,3}[^`]*`{1,3}/g, '')
                .replace(/\[(.*?)\]\([^)]+\)/g, '$1')
                .replace(/[#*_~>]/g, '')
                .replace(/\n+/g, '. ')
                .trim();

            if (!cleanText) return;

            this.currentTtsTurnId = turnId;
            this.allCurrentText = cleanText;
            this.recentSpokenSentences.push({ text: cleanText, timestamp: performance.now() });
            if (this.recentSpokenSentences.length > 10) this.recentSpokenSentences.shift();

            // Split into natural sentences for streaming audio chunking (< 160 chars to avoid browser TTS timeouts)
            const rawSentences = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];
            const chunks = [];
            for (const s of rawSentences) {
                const trimmed = s.trim();
                if (trimmed.length > 160) {
                    const subParts = trimmed.match(/[^,;]+[,;]+|[^,;]+$/g) || [trimmed];
                    for (const sp of subParts) {
                        const subTrimmed = sp.trim();
                        if (subTrimmed.length > 0) chunks.push(subTrimmed);
                    }
                } else if (trimmed.length > 0) {
                    chunks.push(trimmed);
                }
            }
            this.chunkQueue = chunks;

            if (this.chunkQueue.length === 0) return;

            this.isSpeaking = true;
            this.startResumeWatchdog();

            if (this.saviVoice.dotNetRef) {
                this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 5); // 5 = Speaking
            }

            // If we previously called duckAndStop/cancel, give the browser speech daemon 25ms to settle IPC
            const delayMs = wasSpeaking ? 25 : 0;
            const self = this;
            this.pendingSpeakTimeout = setTimeout(() => {
                self.pendingSpeakTimeout = null;
                if (self.isSpeaking) {
                    self.playNextChunk(rate);
                }
            }, delayMs);

        } catch (e) {
            console.error("SAVI playback error:", e);
            this.isSpeaking = false;
            this.stopResumeWatchdog();
            this.currentSpokenText = '';
            this.currentTtsTurnId = null;
            this.lastSpokenTimestamp = performance.now();
            if (this.saviVoice.dotNetRef) {
                this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0);
            }
        }
    }

    playNextChunk(rate) {
        if (!this.isSpeaking) return;

        if (this.chunkQueue.length === 0) {
            this.isSpeaking = false;
            this.isChunkSpeaking = false;
            this.stopResumeWatchdog();
            this.currentSpokenText = '';
            this.lastSpokenTimestamp = performance.now();
            if (this.saviVoice.dotNetRef) {
                const state = this.saviVoice.captureController.continuousVoiceMode ? 1 : 0;
                this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', state);
            }
            return;
        }

        const chunk = this.chunkQueue.shift();
        this.currentSpokenText = chunk;
        this.recentSpokenSentences.push({ text: chunk, timestamp: performance.now() });
        if (this.recentSpokenSentences.length > 10) this.recentSpokenSentences.shift();

        const utterance = new SpeechSynthesisUtterance(chunk);
        utterance.rate = rate || 1.0;
        utterance.pitch = 1.0;
        utterance.volume = 1.0; // Guaranteed full audio volume

        if (!this.availableVoices || this.availableVoices.length === 0) {
            this.loadVoices();
        }

        const preferredVoice = this.availableVoices.find(v =>
            v.lang.startsWith('en') &&
            (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Daniel') || v.name.includes('Samantha') || v.name.includes('Karen') || v.name.includes('Siri') || v.name.includes('Arthur'))
        ) || this.availableVoices.find(v => v.lang.startsWith('en'));

        if (preferredVoice) {
            utterance.voice = preferredVoice;
            utterance.lang = preferredVoice.lang;
        } else {
            utterance.lang = (navigator.language && navigator.language.startsWith('en')) ? navigator.language : 'en-US';
        }

        this.currentUtterance = utterance;
        this.isChunkSpeaking = true;

        // Prevent Chromium / WebKit garbage collection bug
        window._saviActiveUtterances = window._saviActiveUtterances || new Set();
        window._saviActiveUtterances.add(utterance);

        const self = this;
        let chunkHandled = false;

        // Safety watchdog: if synthesis engine hangs, advance chunk after calculated timeout
        const wordCount = chunk.split(/\s+/).length;
        const maxExpectedDurationMs = Math.max(4000, (wordCount / 2.0) * 1000 + 4000);
        const watchdog = setTimeout(() => {
            if (!chunkHandled && self.isSpeaking && self.isChunkSpeaking) {
                console.warn("SAVI: Utterance completion watchdog fired, advancing chunk queue.");
                chunkHandled = true;
                window._saviActiveUtterances.delete(utterance);
                self.isChunkSpeaking = false;
                self.ensureSpeechActive();
                self.playNextChunk(rate);
            }
        }, maxExpectedDurationMs);

        utterance.onend = function () {
            if (chunkHandled) return;
            chunkHandled = true;
            clearTimeout(watchdog);
            window._saviActiveUtterances.delete(utterance);
            self.isChunkSpeaking = false;
            self.lastSpokenTimestamp = performance.now();
            self.playNextChunk(rate);
        };

        utterance.onerror = function (err) {
            if (chunkHandled) return;
            chunkHandled = true;
            clearTimeout(watchdog);
            window._saviActiveUtterances.delete(utterance);
            self.isChunkSpeaking = false;
            self.lastSpokenTimestamp = performance.now();
            console.warn("SAVI: Utterance error, resuming synthesis and playing next chunk:", err?.error || err);
            self.ensureSpeechActive();
            self.playNextChunk(rate);
        };

        // Resume if paused and speak
        this.ensureSpeechActive();
        window.speechSynthesis.speak(utterance);
    }

    // Instant acoustic ducking (<5ms) & playback halt
    duckAndStop(notifyDotNet = true) {
        if (this.pendingSpeakTimeout) {
            clearTimeout(this.pendingSpeakTimeout);
            this.pendingSpeakTimeout = null;
        }

        if (!this.isSpeaking && !window.speechSynthesis?.speaking) return;

        const stopStartTime = performance.now();
        this.isSpeaking = false;
        this.isChunkSpeaking = false;
        this.stopResumeWatchdog();
        this.currentSpokenText = '';
        this.currentTtsTurnId = null;
        this.lastSpokenTimestamp = performance.now();
        this.chunkQueue = [];

        // Instant volume ducking eliminates audio tail-off before cancel executes
        if (this.currentUtterance) {
            try { this.currentUtterance.volume = 0; } catch (_) {}
        }
        if (window._saviActiveUtterances) {
            window._saviActiveUtterances.clear();
        }
        if (window.speechSynthesis) {
            try {
                window.speechSynthesis.cancel();
            } catch (_) {}
        }

        const stopLatencyMs = Math.round(performance.now() - stopStartTime);
        this.lastStopLatencyMs = stopLatencyMs;
        console.log(`SAVI: AudioPlaybackController duckAndStop executed in ${stopLatencyMs}ms`);

        if (notifyDotNet && this.saviVoice.dotNetRef) {
            try {
                this.saviVoice.dotNetRef.invokeMethodAsync('OnUserInterrupted', stopLatencyMs);
                this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 8); // 8 = Interrupted
                setTimeout(() => {
                    if (this.saviVoice.dotNetRef && !this.isSpeaking) {
                        this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                    }
                }, 100);
            } catch (_) {}
        }
    }
}

class AudioCaptureController {
    constructor(saviVoice) {
        this.saviVoice = saviVoice;
        this.capabilities = saviVoice.capabilities;
        this.pipelineInitialized = false;
        this.intentionalStop = false;
        this.recognitionGeneration = 0;
        this.recognitionWatchdog = null;
        this.recognitionRestartTimer = null;
        this.recognitionRestartBackoffMs = 250;
        this.recognitionRestartCount = 0;
        this.recognitionErrors = [];
        this.lastSttEvent = null;
        this.lastError = null;
        this.isListening = false;
        this.continuousVoiceMode = false;
        this.audioOutputMode = 'speaker'; // 'speaker' (aggressive echo filter) | 'headphone' (ultra-sensitive)
        this.hardwareAecEnabled = null;
        this.hardwareNsEnabled = null;
        this.hardwareAgcEnabled = null;
        this.falseSelfDetectionCount = 0;
        this.micStream = null;
        this.audioContext = null;
        this.analyserNode = null;
        this.workletNode = null;
        this.audioWorkletActive = false;

        // Adaptive VAD & Ring Buffer parameters
        this.preRollRingBuffer = null;
        this.preRollWritePtr = 0;
        this.preRollCapacity = 0;
        this.adaptiveNoiseFloor = 0.02;
        this.currentRms = 0.0;
        this.vadSpeechOnsetStartTime = 0;
        this.minSpeechDurationMs = 40; // 40ms sustained speech required to avoid mouth clicks/pops
        this.isSpeechActive = false;

        this.animFrameId = null;
        this.recognition = null;
        this.currentTurn = new UserTurnContext();
        this.seenFinalTranscripts = new Set();
        this.turnTimer = null;

        this.immediateTriggerRegex = /^(?:wait|stop|hold on|actually|no|pause|keep it short|listen|cancel)\b/i;
        this.trailingConjunctionRegex = /\b(?:and|or|because|if|whether|with|that|for|like|so|also|plus|then|but)$/i;
    }

    notifyError(message) {
        if (this.saviVoice.dotNetRef) {
            try { this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceError', message); } catch (_) {}
        }
    }

    recordStt(event, detail = {}) {
        this.lastSttEvent = { event, at: new Date().toISOString(), ...detail };
        this.saviVoice.browserSession?.recordStt(event, detail);
    }

    clearRecognitionWatchdog() {
        if (this.recognitionWatchdog) {
            clearTimeout(this.recognitionWatchdog);
            this.recognitionWatchdog = null;
        }
    }

    armRecognitionWatchdog(generation) {
        this.clearRecognitionWatchdog();
        this.recognitionWatchdog = setTimeout(() => {
            if (generation !== this.recognitionGeneration || this.intentionalStop || !this.isListening) return;
            this.recordStt('stalled', { timeoutMs: 15000 });
            this.restartRecognition('stalled');
        }, 15000);
    }

    restartRecognition(reason = 'ended') {
        if (!this.recognition || this.intentionalStop || !this.continuousVoiceMode) return;
        if (this.recognitionRestartTimer) return;
        const delay = this.recognitionRestartBackoffMs;
        this.recognitionRestartBackoffMs = Math.min(8000, this.recognitionRestartBackoffMs * 2);
        this.recognitionRestartTimer = setTimeout(() => {
            this.recognitionRestartTimer = null;
            if (this.intentionalStop || !this.continuousVoiceMode) return;
            try {
                this.recognition.start();
                this.isListening = true;
                this.recordStt('restart', { reason, delayMs: delay });
                this.saviVoice.browserSession.restartCount = ++this.recognitionRestartCount;
                this.armRecognitionWatchdog(this.recognitionGeneration);
            } catch (error) {
                this.recordStt('restart-error', { reason, name: error?.name });
                this.restartRecognition('retry');
            }
        }, delay);
    }

    async initAudioPipeline(requestPermission = false) {
        const caps = this.saviVoice.capabilities;
        if (!caps?.speechRecognition) return false;
        if (!requestPermission) {
            this.initSpeechRecognition();
            return true;
        }
        if (this.pipelineInitialized) return true;

        try {
            const audio = this.saviVoice.browserAudio;
            const diagnostics = await audio.acquire();
            this.micStream = audio.stream;
            this.audioContext = audio.context;
            this.analyserNode = audio.analyser;
            this.audioWorkletActive = audio.audioWorkletActive;

            const sampleRate = this.audioContext?.sampleRate || diagnostics.sampleRate || 48000;
            this.preRollCapacity = Math.max(1, Math.round(sampleRate * 0.3));
            this.preRollRingBuffer = new Float32Array(this.preRollCapacity);
            this.preRollWritePtr = 0;

            this.hardwareAecEnabled = diagnostics.aec === 'Enabled';
            this.hardwareNsEnabled = diagnostics.ns === 'Enabled';
            this.hardwareAgcEnabled = diagnostics.agc === 'Enabled';
            this.saviVoice.browserSession.recordAudio('capture-acquired', diagnostics);

            await audio.connectAnalyser(samples => this.handleAudioFrame(samples));
            this.analyserNode = audio.analyser;
            this.audioContext = audio.context;
            this.audioWorkletActive = audio.audioWorkletActive;

            if (this.saviVoice.dotNetRef) {
                try {
                    await this.saviVoice.dotNetRef.invokeMethodAsync(
                        'OnAudioSettingsDetected',
                        this.hardwareAecEnabled,
                        this.hardwareNsEnabled,
                        this.hardwareAgcEnabled,
                        sampleRate
                    );
                } catch (_) {}
            }

            this.startVadVisualizerLoop();
            this.pipelineInitialized = true;
            this.initSpeechRecognition();
            return true;
        } catch (err) {
            this.lastError = err;
            this.saviVoice.browserSession.recordAudio('capture-error', { name: err?.name, message: err?.message });
            const message = err?.name === 'NotAllowedError' || err?.name === 'SecurityError'
                ? 'Microphone permission was denied. Allow microphone access for SAVI, then press Start Voice again.'
                : err?.name === 'NotSupportedError'
                    ? 'Voice input is not available in this browser. Please use Chat mode or a supported browser.'
                    : 'Voice input could not start. Press Start Voice to retry.';
            this.notifyError(message);
            return false;
        }
    }

    handleAudioFrame(inputSamples) {
        if (!inputSamples || inputSamples.length === 0) return;

        // 1. Write to Circular Pre-Roll Buffer
        const n = inputSamples.length;
        for (let i = 0; i < n; i++) {
            this.preRollRingBuffer[this.preRollWritePtr] = inputSamples[i];
            this.preRollWritePtr = (this.preRollWritePtr + 1) % this.preRollCapacity;
        }

        // 2. Compute RMS Energy of Frame
        let sumSq = 0;
        for (let i = 0; i < n; i++) {
            sumSq += inputSamples[i] * inputSamples[i];
        }
        const rms = Math.sqrt(sumSq / n);
        this.currentRms = rms;

        // 3. Adaptive Noise Floor Tracking (smooth EMA when user is not speaking)
        if (!this.isSpeechActive) {
            this.adaptiveNoiseFloor = (this.adaptiveNoiseFloor * 0.95) + (rms * 0.05);
        }

        // 4. Adaptive VAD with Hysteresis and Speaker-Aware Dynamic Threshold
        const isAssistantSpeaking = this.saviVoice.playbackController.isSpeaking;

        // When assistant is speaking through device speakers, acoustic energy leaks into the microphone
        // Dynamic onset threshold:
        // - Speaker Mode while assistant speaks: threshold is elevated to 0.36 or baseline + 0.16
        // - Headphone Mode or assistant silent: sensitive threshold (0.12 or baseline + 0.04)
        const onsetThreshold = isAssistantSpeaking
            ? (this.audioOutputMode === 'headphone' ? Math.max(0.12, this.adaptiveNoiseFloor + 0.04) : Math.max(0.36, this.adaptiveNoiseFloor + 0.16))
            : Math.max(0.12, this.adaptiveNoiseFloor + 0.04);

        const continuationThreshold = onsetThreshold * 0.70;

        const now = performance.now();

        if (!this.isSpeechActive) {
            if (rms >= onsetThreshold) {
                if (!this.vadSpeechOnsetStartTime) {
                    this.vadSpeechOnsetStartTime = now;
                } else if (now - this.vadSpeechOnsetStartTime >= this.minSpeechDurationMs) {
                    // Confirmed User Speech! Single Authoritative Event for Barge-In
                    this.isSpeechActive = true;
                    this.vadSpeechOnsetStartTime = 0;
                    this.currentTurn.hasPreRollAudio = true;

                    if (isAssistantSpeaking) {
                        console.log("SAVI VAD: Sustained user speech detected -> Instant barge-in halt!");
                        this.saviVoice.playbackController.duckAndStop(true);
                    }

                    if (this.saviVoice.dotNetRef) {
                        this.saviVoice.dotNetRef.invokeMethodAsync('OnVadSpeechStarted', now);
                    }
                }
            } else {
                this.vadSpeechOnsetStartTime = 0;
            }
        } else {
            // Speech currently active: use lower continuation threshold
            if (rms < continuationThreshold) {
                // Speech energy dropped below continuation threshold
                this.isSpeechActive = false;
                this.vadSpeechOnsetStartTime = 0;
            }
        }
    }

    startVadVisualizerLoop() {
        const self = this;
        const dataArray = new Uint8Array(this.analyserNode ? this.analyserNode.frequencyBinCount : 0);

        function checkFrame() {
            if (self.analyserNode) {
                self.analyserNode.getByteFrequencyData(dataArray);

                let sum = 0;
                for (let i = 0; i < dataArray.length; i++) {
                    sum += dataArray[i];
                }
                const avg = dataArray.length > 0 ? sum / dataArray.length : 0;
                const normalizedLevel = Math.min(1.0, Math.max(0.0, avg / 128.0));

                // Update CSS variable --savi-audio-level for reactive waveform and glowing rings
                document.documentElement.style.setProperty('--savi-audio-level', normalizedLevel.toFixed(3));
            }

            self.animFrameId = requestAnimationFrame(checkFrame);
        }

        checkFrame();
    }

    // Secondary client-side self-echo detector
    isSelfEcho(transcript) {
        if (!transcript || typeof transcript !== 'string') return false;
        const cleanT = transcript.trim().toLowerCase().replace(/[.,/#!$%^&*;:{}=\-_`~()?"']/g, "").replace(/\s+/g, " ");
        if (!cleanT) return false;

        // In headphone mode, zero acoustic speaker-to-mic coupling exists
        if (this.audioOutputMode === 'headphone') return false;

        const playback = this.saviVoice.playbackController;
        const isSpeaking = playback.isSpeaking;
        const timeSinceSpeech = performance.now() - (playback.lastSpokenTimestamp || 0);
        const inEchoWindow = isSpeaking || (timeSinceSpeech < 1800);

        if (!inEchoWindow) return false;

        // Explicit barge-in command words: NEVER treat as echo!
        const isBargeInCommand = /^(?:wait|stop|hold on|actually|no|pause|listen|cancel|quiet|never mind|shh|shut up|hey savi|thats wrong|that's wrong|i meant|why|and tomorrow)\b/i.test(cleanT);
        if (isBargeInCommand) {
            return false;
        }

        const tTokens = cleanT.split(' ').filter(w => w.length > 1);
        if (tTokens.length === 0) return false;

        // Compare against currently spoken text, full turn text, and recent spoken sentences
        const candidates = [];
        if (playback.currentSpokenText) candidates.push(playback.currentSpokenText);
        if (playback.allCurrentText) candidates.push(playback.allCurrentText);
        for (const s of playback.recentSpokenSentences) {
            if (s.text) candidates.push(s.text);
        }

        for (const cand of candidates) {
            const cleanCand = cand.toLowerCase().replace(/[.,/#!$%^&*;:{}=\-_`~()?"']/g, "").replace(/\s+/g, " ");
            if (!cleanCand) continue;

            // Direct substring containment
            if (cleanCand.includes(cleanT) || cleanT.includes(cleanCand)) {
                return true;
            }

            // Word token overlap calculation
            const candTokens = new Set(cleanCand.split(' ').filter(w => w.length > 1));
            let matchCount = 0;
            for (const tok of tTokens) {
                if (candTokens.has(tok)) matchCount++;
            }

            const overlapRatio = matchCount / tTokens.length;
            // Suppress only if >=60% overlap and at least 3 matching words
            if (overlapRatio >= 0.60 && matchCount >= 3) {
                return true;
            }
        }

        return false;
    }

    initSpeechRecognition() {
        const SpeechRecognition = this.capabilities?.speechRecognitionImplementation === 'SpeechRecognition'
            ? window.SpeechRecognition
            : window.webkitSpeechRecognition || window.SpeechRecognition;
        if (!SpeechRecognition) return;
        if (this.recognition) return;

        try {
            this.recognition = new SpeechRecognition();
            const generation = ++this.recognitionGeneration;
            this.recognition.continuous = true;
            this.recognition.interimResults = true;
            this.recognition.maxAlternatives = 1;
            this.recognition.lang = navigator.language || 'en-US';

            const self = this;

            this.recognition.onstart = function () {
                if (generation !== self.recognitionGeneration) return;
                self.isListening = true;
                self.intentionalStop = false;
                self.recognitionRestartBackoffMs = 250;
                self.recordStt('start');
                self.armRecognitionWatchdog(generation);
                self.saviVoice.browserSession.listening();
                if (self.saviVoice.dotNetRef) {
                    self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                }
            };

            this.recognition.onresult = function (event) {
                if (generation !== self.recognitionGeneration) return;
                self.recordStt('result', { resultIndex: event.resultIndex });
                self.armRecognitionWatchdog(generation);
                let interimText = '';
                let finalChunk = '';

                for (let i = event.resultIndex; i < event.results.length; ++i) {
                    const res = event.results[i];
                    const rawText = res[0].transcript;

                    // SELF-ECHO FILTER: Reject transcript if it's SAVI's own voice coming out of device speakers!
                    if (self.isSelfEcho(rawText)) {
                        self.falseSelfDetectionCount++;
                        console.log(`[SAVI Self-Echo Suppressed #${self.falseSelfDetectionCount}]: "${rawText.trim()}"`);
                        if (self.saviVoice.dotNetRef) {
                            self.saviVoice.dotNetRef.invokeMethodAsync('OnSelfEchoSuppressed', rawText.trim(), self.falseSelfDetectionCount);
                        }
                        continue; // DISCARD ECHO!
                    }

                    if (res.isFinal) {
                        const signature = `${self.currentTurn.turnId}:${rawText.trim().toLowerCase()}`;
                        if (!self.seenFinalTranscripts.has(signature)) {
                            self.seenFinalTranscripts.add(signature);
                            finalChunk += rawText + ' ';
                        }
                    } else {
                        interimText += rawText;
                    }
                }

                // If all audio in this batch was SAVI's own voice echoing, do not proceed!
                if (!finalChunk.trim() && !interimText.trim()) {
                    return;
                }

                // Note: Barge-in playback ducking is already handled authoritatively by VAD SpeechStarted!
                // We do NOT call duckAndStop() here to avoid duplicate triggers and race conditions.

                if (finalChunk) {
                    self.currentTurn.finalTranscript += finalChunk;
                }
                self.currentTurn.partialTranscript = interimText;

                const fullText = self.currentTurn.getFullText();

                if (interimText && self.saviVoice.dotNetRef) {
                    self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 7); // 7 = DetectingSpeech
                    self.saviVoice.dotNetRef.invokeMethodAsync('OnSpeechInterim', fullText);
                }

                if (self.turnTimer) {
                    clearTimeout(self.turnTimer);
                    self.turnTimer = null;
                }

                if (fullText) {
                    const trimmedUtterance = fullText.replace(/[.,!?;:]+$/, '');
                    const endsWithConjunction = self.trailingConjunctionRegex.test(trimmedUtterance);
                    const isImmediate = self.immediateTriggerRegex.test(fullText);

                    const dispatchFinalTurn = function () {
                        if (self.turnTimer) {
                            clearTimeout(self.turnTimer);
                            self.turnTimer = null;
                        }
                        const finalUtterance = self.currentTurn.getFullText();
                        const turnId = self.currentTurn.turnId;

                        if (finalUtterance && !self.currentTurn.hasDispatched) {
                            self.currentTurn.hasDispatched = true;
                            self.currentTurn = new UserTurnContext(); // Fresh turn context for next interaction

                            if (self.saviVoice.dotNetRef) {
                                self.saviVoice.dotNetRef.invokeMethodAsync('OnSpeechRecognized', finalUtterance, turnId);
                            }
                        }
                    };

                    if (isImmediate && fullText.split(/\s+/).length <= 4) {
                        // Instant commands (wait, stop, hold on) trigger with zero pause delay
                        dispatchFinalTurn();
                    } else {
                        // 1200ms pause for conjunctions; 700ms standard pause; 1000ms interim-only pause
                        const delayMs = endsWithConjunction ? 1200 : (finalChunk ? 700 : 1000);
                        self.turnTimer = setTimeout(dispatchFinalTurn, delayMs);
                    }
                }
            };

            this.recognition.onerror = function (event) {
                if (generation !== self.recognitionGeneration) return;
                self.recordStt('error', { error: event.error });
                self.recognitionErrors.push({ error: event.error, at: new Date().toISOString() });
                self.saviVoice.browserSession.recognitionErrors = self.recognitionErrors.slice(-8);
                if (event.error === 'no-speech') {
                    self.armRecognitionWatchdog(generation);
                    return;
                }
                console.warn("SAVI Speech Recognition Error:", event.error);

                if (self.saviVoice.dotNetRef) {
                    let msg = event.error === 'not-allowed' || event.error === 'service-not-allowed'
                        ? "Microphone permission was denied. Allow microphone access for SAVI, then press Start Voice again."
                        : "Voice recognition encountered a temporary error. SAVI will retry safely.";
                    if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
                        self.intentionalStop = true;
                    }
                    self.notifyError(msg);
                }
            };

            this.recognition.onend = function () {
                if (generation !== self.recognitionGeneration) return;
                self.recordStt('end');
                self.clearRecognitionWatchdog();
                self.isListening = false;
                const fullText = self.currentTurn.getFullText();

                // Dispatch any accumulated speech before restarting
                if (fullText && !self.currentTurn.hasDispatched) {
                    if (self.turnTimer) {
                        clearTimeout(self.turnTimer);
                        self.turnTimer = null;
                    }
                    const turnId = self.currentTurn.turnId;
                    self.currentTurn.hasDispatched = true;
                    self.currentTurn = new UserTurnContext();

                    if (self.saviVoice.dotNetRef) {
                        self.saviVoice.dotNetRef.invokeMethodAsync('OnSpeechRecognized', fullText, turnId);
                    }
                }

                // In continuous mode, restart STT stream seamlessly without touching microphone capture or AudioContext
                if (self.continuousVoiceMode && !self.intentionalStop) {
                    self.restartRecognition('ended');
                } else {
                    if (self.saviVoice.dotNetRef) {
                        self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // 0 = Idle
                    }
                }
            };
        } catch (err) {
            console.error("SAVI initSpeechRecognition error:", err);
            this.notifyError('Voice recognition is unavailable in this browser. Please use Chat mode or a supported browser.');
        }
    }

    async start() {
        this.intentionalStop = false;
        this.continuousVoiceMode = this.continuousVoiceMode || false;
        this.saviVoice.browserSession.start(this.saviVoice.conversationId);
        const ready = await this.initAudioPipeline(true);
        if (!ready || !this.recognition) {
            this.notifyError('Voice recognition isn\'t available in this browser. Please use Chat mode or a supported browser.');
            return false;
        }
        if (this.audioContext?.state === 'suspended') {
            try { await this.audioContext.resume(); } catch (_) {}
        }
        if (!this.isListening) {
            try {
                this.currentTurn = new UserTurnContext();
                this.recognition.start();
                this.isListening = true;
                this.saviVoice.browserSession.listening();
                this.armRecognitionWatchdog(this.recognitionGeneration);
            } catch (e) {
                if (e.name === 'InvalidStateError') {
                    this.isListening = true;
                } else {
                    this.notifyError('Voice recognition could not start. Press Start Voice to retry.');
                    return false;
                }
            }
        }
        return true;
    }

    stop() {
        this.continuousVoiceMode = false;
        this.intentionalStop = true;
        this.clearRecognitionWatchdog();
        if (this.recognitionRestartTimer) {
            clearTimeout(this.recognitionRestartTimer);
            this.recognitionRestartTimer = null;
        }
        if (this.recognition && this.isListening) {
            try { this.recognition.stop(); } catch (_) {}
        }
        this.isListening = false;
        this.saviVoice.browserSession.stop();
    }
}

// Global SAVI Voice Facade
window.saviVoice = {
    dotNetRef: null,
    captureController: null,
    playbackController: null,
    capabilities: null,
    browserAudio: null,
    browserSession: null,
    conversationId: null,
    lifecycleBound: false,

    isSupported: function () {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    },

    init: async function (dotNetHelper, conversationId = null) {
        this.dotNetRef = dotNetHelper;
        const browser = window.saviBrowser.init();
        this.capabilities = browser.capabilities.snapshot;
        this.browserAudio = browser.audio;
        this.browserSession = browser.voiceSession;
        this.conversationId = conversationId || this.conversationId;
        this.browserSession.conversationId = this.conversationId;

        this.playbackController ??= new AudioPlaybackController(this);
        this.captureController ??= new AudioCaptureController(this);
        this.captureController.capabilities = this.capabilities;

        // Detect and prepare recognition without opening the microphone. A
        // user gesture is required before getUserMedia() is called.
        await this.captureController.initAudioPipeline(false);
        await this.browserSession.capabilities.refreshPermissionState();
        this.setupLifecycleHandlers();
        this.setupKeyboardShortcuts();
        return !!(this.capabilities.speechRecognition && this.capabilities.getUserMedia);
    },

    setConversationId: function (conversationId) {
        this.conversationId = conversationId || null;
        if (this.browserSession) this.browserSession.conversationId = this.conversationId;
    },

    setContinuousMode: async function (enabled) {
        if (!this.captureController) return false;
        this.captureController.continuousVoiceMode = enabled;
        if (enabled) {
            return await this.captureController.start();
        } else {
            this.captureController.stop();
            this.playbackController?.duckAndStop(false);
            return true;
        }
    },

    setAudioOutputMode: function (mode) {
        if (this.captureController) {
            this.captureController.audioOutputMode = mode;
            console.log("SAVI Audio Output Mode configured to:", mode);
        }
    },

    getDiagnostics: function () {
        return {
            ...(window.saviBrowser?.diagnostics?.() || {}),
            session: this.browserSession?.diagnostics?.() || null,
            audio: this.browserAudio?.diagnostics?.() || null,
            micActive: this.captureController?.isListening ?? false,
            speaking: this.playbackController?.isSpeaking ?? false,
            audioOutputMode: this.captureController?.audioOutputMode ?? 'speaker',
            hardwareAec: this.captureController?.hardwareAecEnabled,
            hardwareNs: this.captureController?.hardwareNsEnabled,
            hardwareAgc: this.captureController?.hardwareAgcEnabled,
            selfEchoSuppressedCount: this.captureController?.falseSelfDetectionCount ?? 0,
            noiseFloor: this.captureController?.adaptiveNoiseFloor ?? 0.02,
            currentRms: this.captureController?.currentRms ?? 0.0,
            lastInterruptionLatencyMs: this.playbackController?.lastStopLatencyMs ?? 0,
            audioWorkletActive: this.captureController?.audioWorkletActive ?? false
        };
    },

    getDiagnosticsJson: function () {
        return JSON.stringify(this.getDiagnostics(), null, 2);
    },

    toggleListening: async function () {
        if (!this.captureController) {
            const ok = await this.init(this.dotNetRef);
            if (!ok) return false;
        }

        if (this.captureController.isListening) {
            this.captureController.stop();
            return false;
        } else {
            return await this.captureController.start();
        }
    },

    startListening: async function () {
        if (this.captureController) {
            return await this.captureController.start();
        }
        return false;
    },

    stopListening: function () {
        if (this.captureController) {
            this.captureController.stop();
            return false;
        }
        return false;
    },

    shutdown: async function () {
        this.captureController?.stop();
        this.playbackController?.duckAndStop(false);
        await this.browserAudio?.close?.();
        if (this.browserSession) this.browserSession.stop();
        this.captureController = null;
        this.playbackController = null;
        this.browserAudio = null;
        this.browserSession = null;
    },

    setupLifecycleHandlers: function () {
        if (this.lifecycleBound) return;
        this.lifecycleBound = true;
        const record = (event, detail = {}) => {
            this.browserSession?.recordAudio?.(event, detail);
        };
        document.addEventListener('visibilitychange', () => {
            record(document.visibilityState === 'hidden' ? 'visibility-hidden' : 'visibility-visible');
        });
        window.addEventListener('pagehide', () => record('pagehide'));
        window.addEventListener('pageshow', () => {
            record('pageshow');
            const track = this.browserAudio?.stream?.getAudioTracks?.()[0];
            if (track && track.readyState === 'ended' && this.captureController?.isListening) {
                this.captureController.notifyError('Microphone access ended. Press Start Voice to reconnect it.');
            }
        });
        window.addEventListener('focus', () => record('focus'));
        window.addEventListener('blur', () => record('blur'));
        const updateViewport = () => {
            if (this.capabilities?.viewport) {
                this.capabilities.viewport.width = window.innerWidth;
                this.capabilities.viewport.height = window.innerHeight;
                this.capabilities.viewport.visualHeight = window.visualViewport?.height || window.innerHeight;
                this.capabilities.viewport.orientation = window.screen?.orientation?.type || 'unknown';
            }
            record('viewport-change', { width: window.innerWidth, height: window.innerHeight });
        };
        window.addEventListener('resize', updateViewport, { passive: true });
        window.addEventListener('orientationchange', updateViewport, { passive: true });
    },

    speak: function (text, rate = 1.0, turnId = null) {
        if (this.playbackController) {
            this.playbackController.speak(text, rate, turnId);
        }
    },

    stopSpeaking: function () {
        if (this.playbackController) {
            this.playbackController.duckAndStop(false);
        }
    },

    interruptSpeaking: function () {
        if (this.playbackController) {
            this.playbackController.duckAndStop(true);
        }
    },

    setupKeyboardShortcuts: function () {
        const self = this;
        let spacePressed = false;

        window.addEventListener('keydown', function (e) {
            // Escape key: Immediate Silence / Interruption
            if (e.key === 'Escape') {
                if (self.playbackController?.isSpeaking) {
                    self.interruptSpeaking();
                } else if (self.captureController?.isListening && !self.captureController?.continuousVoiceMode) {
                    self.stopListening();
                }
            }

            // Space key: Push-To-Talk (when not in text input)
            if (e.code === 'Space' && !spacePressed) {
                const tag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
                if (tag !== 'input' && tag !== 'textarea' && !document.activeElement?.isContentEditable) {
                    spacePressed = true;
                    if (self.playbackController?.isSpeaking) self.interruptSpeaking();
                    if (!self.captureController?.isListening) self.startListening();
                }
            }
        });

        window.addEventListener('keyup', function (e) {
            if (e.code === 'Space' && spacePressed) {
                spacePressed = false;
                const tag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
                if (tag !== 'input' && tag !== 'textarea') {
                    if (self.captureController?.isListening && !self.captureController?.continuousVoiceMode) {
                        self.stopListening();
                    }
                }
            }
        });
    },

    autoSpeakGreeting: function (text) {
        const self = this;
        let speechTriggered = false;

        const doSpeak = function () {
            if (speechTriggered) return;
            try {
                if (window.speechSynthesis && window.speechSynthesis.paused) {
                    window.speechSynthesis.resume();
                }
                self.speak(text);
                speechTriggered = true;
            } catch (e) {
                console.warn("SAVI autoSpeakGreeting failed:", e);
            }
        };

        if (window.speechSynthesis) {
            if (window.speechSynthesis.getVoices().length === 0) {
                const prev = window.speechSynthesis.onvoiceschanged;
                window.speechSynthesis.onvoiceschanged = function () {
                    self.playbackController?.loadVoices();
                    if (prev) prev();
                    doSpeak();
                };
            } else {
                doSpeak();
            }
        }

        const onUserGesture = function () {
            if (!speechTriggered || (window.speechSynthesis && !window.speechSynthesis.speaking)) {
                doSpeak();
            }
            ['click', 'touchstart', 'pointerdown', 'keydown'].forEach(evt => {
                window.removeEventListener(evt, onUserGesture, true);
            });
        };

        ['click', 'touchstart', 'pointerdown', 'keydown'].forEach(evt => {
            window.addEventListener(evt, onUserGesture, { once: true, capture: true });
        });
    },

    testAudio: function () {
        if (this.playbackController) {
            this.speak("Audio output is loud and clear, Shatru. All neural speech channels are active.");
        }
    }
};

// Global Mobile Audio Unlocking: iOS Safari & Android Chrome require user touch/click gesture to activate audio output
(function () {
    if (typeof window === 'undefined') return;

    function unlockMobileAudio() {
        if (window.speechSynthesis) {
            try {
                if (window.speechSynthesis.paused) window.speechSynthesis.resume();
            } catch (_) {}
        }

        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (AudioCtx && window.saviVoice?.captureController?.audioContext) {
            const ctx = window.saviVoice.captureController.audioContext;
            if (ctx.state === 'suspended') {
                try { ctx.resume(); } catch (_) {}
            }
            try {
                const buf = ctx.createBuffer(1, 1, 22050);
                const src = ctx.createBufferSource();
                src.buffer = buf;
                src.connect(ctx.destination);
                src.start(0);
            } catch (_) {}
        }
    }

    ['click', 'touchstart', 'touchend', 'pointerdown', 'keydown'].forEach(evt => {
        window.addEventListener(evt, unlockMobileAudio, { capture: true, passive: true });
    });
})();
