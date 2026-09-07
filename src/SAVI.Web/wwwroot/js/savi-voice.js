// SAVI Voice Engine — Full-Duplex Real-Time Voice Conversation Architecture
// Decoupled AudioCaptureController & AudioPlaybackController
// 250ms Rolling Audio Pre-Roll Buffer, Client-Side VAD, and Atomic Per-Turn Context

class UserTurnContext {
    constructor() {
        this.turnId = 'turn_' + Math.random().toString(36).substring(2, 9) + '_' + Date.now();
        this.partialTranscript = '';
        this.finalTranscript = '';
        this.startTime = performance.now();
        this.endTime = null;
        this.hasDispatched = false;
        this.isInterrupted = false;
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
        this.recentSpokenSentences = []; // Rolling buffer of recent chunks with timestamps

        this.loadVoices();
        if (window.speechSynthesis) {
            window.speechSynthesis.onvoiceschanged = () => this.loadVoices();
        }
    }

    loadVoices() {
        if (window.speechSynthesis) {
            this.availableVoices = window.speechSynthesis.getVoices();
        }
    }

    speak(text, rate = 1.0, turnId = null) {
        if (!window.speechSynthesis) return;

        try {
            // Duck and halt any prior speech immediately
            this.duckAndStop(false);

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

            // Split into natural sentences for streaming audio chunking
            const chunks = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];
            this.chunkQueue = chunks.map(c => c.trim()).filter(c => c.length > 0);

            if (this.chunkQueue.length === 0) return;

            this.isSpeaking = true;
            if (this.saviVoice.dotNetRef) {
                this.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 5); // 5 = Speaking
            }

            this.playNextChunk(rate);
        } catch (e) {
            console.error("SAVI playback error:", e);
            this.isSpeaking = false;
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

        if (!this.availableVoices || this.availableVoices.length === 0) {
            this.loadVoices();
        }

        const preferredVoice = this.availableVoices.find(v =>
            v.lang.startsWith('en') &&
            (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Daniel') || v.name.includes('Samantha') || v.name.includes('Karen') || v.name.includes('Siri'))
        ) || this.availableVoices.find(v => v.lang.startsWith('en'));

        if (preferredVoice) utterance.voice = preferredVoice;

        this.currentUtterance = utterance;
        this.isChunkSpeaking = true;

        const self = this;
        utterance.onend = function () {
            self.isChunkSpeaking = false;
            self.lastSpokenTimestamp = performance.now();
            self.playNextChunk(rate);
        };

        utterance.onerror = function () {
            self.isChunkSpeaking = false;
            self.lastSpokenTimestamp = performance.now();
            self.playNextChunk(rate);
        };

        window.speechSynthesis.speak(utterance);
    }

    // Instant acoustic ducking (<5ms) & playback halt
    duckAndStop(notifyDotNet = true) {
        if (!this.isSpeaking && !window.speechSynthesis?.speaking) return;

        const stopStartTime = performance.now();
        this.isSpeaking = false;
        this.isChunkSpeaking = false;
        this.currentSpokenText = '';
        this.currentTtsTurnId = null;
        this.lastSpokenTimestamp = performance.now();
        this.chunkQueue = [];

        // Instant volume ducking eliminates audio tail-off before cancel executes
        if (this.currentUtterance) {
            try { this.currentUtterance.volume = 0; } catch (_) {}
        }
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }

        const stopLatencyMs = Math.round(performance.now() - stopStartTime);
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
        this.preRollRingBuffer = null;
        this.preRollWritePtr = 0;
        this.vadSpeechCounter = 0;
        this.animFrameId = null;
        this.recognition = null;
        this.currentTurn = new UserTurnContext();
        this.turnTimer = null;

        this.immediateTriggerRegex = /^(?:wait|stop|hold on|actually|no|pause|keep it short)\b/i;
        this.trailingConjunctionRegex = /\b(?:and|or|because|if|whether|with|that|for|like|so|also|plus|then|but)$/i;
    }

    async initAudioPipeline() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return false;
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return false;

            if (!this.audioContext) {
                this.audioContext = new AudioCtx();
            }

            if (this.audioContext.state === 'suspended') {
                const resumeHandler = () => {
                    if (this.audioContext && this.audioContext.state === 'suspended') {
                        this.audioContext.resume();
                    }
                    ['click', 'touchstart', 'keydown'].forEach(e => window.removeEventListener(e, resumeHandler, true));
                };
                ['click', 'touchstart', 'keydown'].forEach(e => window.addEventListener(e, resumeHandler, { once: true, capture: true }));
            }

            if (!this.micStream) {
                this.micStream = await navigator.mediaDevices.getUserMedia({
                    audio: {
                        echoCancellation: true,
                        noiseSuppression: true,
                        autoGainControl: true
                    }
                });
            }

            // Inspect actual MediaStreamTrack settings to verify hardware AEC & NS
            const audioTracks = this.micStream.getAudioTracks();
            if (audioTracks && audioTracks.length > 0) {
                const track = audioTracks[0];
                const settings = track.getSettings ? track.getSettings() : {};
                this.hardwareAecEnabled = settings.echoCancellation !== false;
                this.hardwareNsEnabled = settings.noiseSuppression !== false;
                this.hardwareAgcEnabled = settings.autoGainControl !== false;
                console.log("SAVI Hardware Audio Diagnostics:", {
                    echoCancellation: settings.echoCancellation,
                    noiseSuppression: settings.noiseSuppression,
                    autoGainControl: settings.autoGainControl,
                    sampleRate: settings.sampleRate,
                    channelCount: settings.channelCount
                });

                if (this.saviVoice.dotNetRef) {
                    try {
                        this.saviVoice.dotNetRef.invokeMethodAsync(
                            'OnAudioSettingsDetected',
                            !!this.hardwareAecEnabled,
                            !!this.hardwareNsEnabled,
                            !!this.hardwareAgcEnabled,
                            settings.sampleRate || 48000
                        );
                    } catch (_) {}
                }
            }

            const source = this.audioContext.createMediaStreamSource(this.micStream);
            this.analyserNode = this.audioContext.createAnalyser();
            this.analyserNode.fftSize = 128;
            this.analyserNode.smoothingTimeConstant = 0.8;
            source.connect(this.analyserNode);

            // Circular Pre-Roll Ring Buffer: stores ~300ms of rolling audio samples
            const ringSamples = Math.round(this.audioContext.sampleRate * 0.3);
            this.preRollRingBuffer = new Float32Array(ringSamples);
            this.preRollWritePtr = 0;

            if (this.audioContext.createScriptProcessor) {
                const proc = this.audioContext.createScriptProcessor(1024, 1, 1);
                const self = this;
                proc.onaudioprocess = function (e) {
                    const input = e.inputBuffer.getChannelData(0);
                    for (let i = 0; i < input.length; i++) {
                        self.preRollRingBuffer[self.preRollWritePtr] = input[i];
                        self.preRollWritePtr = (self.preRollWritePtr + 1) % ringSamples;
                    }
                };

                const silentGain = this.audioContext.createGain();
                silentGain.gain.value = 0;
                source.connect(proc);
                proc.connect(silentGain);
                silentGain.connect(this.audioContext.destination);
            }

            this.startVadVisualizerLoop();
            this.initSpeechRecognition();
            return true;
        } catch (err) {
            console.warn("SAVI AudioCaptureController initialization warning:", err.message);
            return false;
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

                // Dynamic VAD Interruption:
                // When speaking on laptop speakers, physical speaker sound couples into the mic at ~0.20-0.35.
                // In Speaker Mode: Elevate threshold to 0.42 and require 3 sustained frames so SAVI does not self-interrupt!
                // In Headphone Mode: Zero acoustic coupling; threshold remains sensitive (0.20).
                const isSpeaking = self.saviVoice.playbackController.isSpeaking;
                const dynamicThreshold = isSpeaking
                    ? (self.audioOutputMode === 'headphone' ? 0.20 : 0.42)
                    : 0.18;

                if (isSpeaking && normalizedLevel > dynamicThreshold) {
                    self.vadSpeechCounter++;
                    if (self.vadSpeechCounter >= 3) { // ~45ms of sustained speech energy clearly above speaker spillover
                        console.log("SAVI VAD: Sustained user voice above speaker level detected -> Instant duck & stop!");
                        self.saviVoice.playbackController.duckAndStop(true);
                        self.vadSpeechCounter = 0;

                        if (self.saviVoice.dotNetRef) {
                            self.saviVoice.dotNetRef.invokeMethodAsync('OnVadSpeechStarted', performance.now());
                        }
                    }
                } else {
                    self.vadSpeechCounter = 0;
                }
            }

            self.animFrameId = requestAnimationFrame(checkFrame);
        }

        checkFrame();
    }

    // Intelligent client-side self-echo detector
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
        const isBargeInCommand = /^(?:wait|stop|hold on|actually|no|pause|listen|cancel|quiet|never mind|shh|shut up|hey savi)\b/i.test(cleanT);
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

            // Direct substring containment (e.g. "weather in Pune" matches "The weather in Pune today is around 28 degrees")
            if (cleanCand.includes(cleanT) || cleanT.includes(cleanCand)) {
                return true;
            }

            // Word token overlap / Jaccard calculation
            const candTokens = new Set(cleanCand.split(' ').filter(w => w.length > 1));
            let matchCount = 0;
            for (const tok of tTokens) {
                if (candTokens.has(tok)) matchCount++;
            }

            const overlapRatio = matchCount / tTokens.length;
            // If >=50% of the recognized words are found in SAVI's speech, it is an acoustic echo!
            if (overlapRatio >= 0.50) {
                return true;
            }
        }

        return false;
    }

    initSpeechRecognition() {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) return;

        try {
            this.recognition = new SpeechRecognition();
            this.recognition.continuous = true;
            this.recognition.interimResults = true;
            this.recognition.maxAlternatives = 1;
            this.recognition.lang = navigator.language || 'en-US';

            const self = this;

            this.recognition.onstart = function () {
                self.isListening = true;
                if (self.saviVoice.dotNetRef) {
                    self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                }
            };

            this.recognition.onresult = function (event) {
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
                        finalChunk += rawText + ' ';
                    } else {
                        interimText += rawText;
                    }
                }

                // If all audio in this batch was SAVI's own voice echoing, do not duck and do not dispatch!
                if (!finalChunk.trim() && !interimText.trim()) {
                    return;
                }

                // Genuinely new user speech detected! Duck playback immediately!
                if (self.saviVoice.playbackController.isSpeaking) {
                    console.log("SAVI: Genuine user speech heard during speech -> Instant duck & stop!");
                    self.saviVoice.playbackController.duckAndStop(true);
                }

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
                        // Instant commands (wait, stop, keep it short) trigger with zero pause delay
                        dispatchFinalTurn();
                    } else {
                        // 1200ms pause for conjunctions; 700ms standard pause; 1000ms interim-only pause
                        const delayMs = endsWithConjunction ? 1200 : (finalChunk ? 700 : 1000);
                        self.turnTimer = setTimeout(dispatchFinalTurn, delayMs);
                    }
                }
            };

            this.recognition.onerror = function (event) {
                if (event.error === 'no-speech') return; // Silence in continuous mode is normal
                console.warn("SAVI Speech Recognition Error:", event.error);

                if (self.saviVoice.dotNetRef) {
                    let msg = event.error === 'not-allowed' ? "Microphone permission denied." : "Voice error: " + event.error;
                    self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceError', msg);
                }
            };

            this.recognition.onend = function () {
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

                // In continuous mode, restart STT stream seamlessly without touching microphone capture
                if (self.continuousVoiceMode) {
                    setTimeout(() => {
                        if (self.continuousVoiceMode && !self.isListening) {
                            try {
                                self.recognition.start();
                                self.isListening = true;
                            } catch (_) {}
                        }
                    }, 50);
                } else {
                    if (self.saviVoice.dotNetRef) {
                        self.saviVoice.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // 0 = Idle
                    }
                }
            };
        } catch (err) {
            console.error("SAVI initSpeechRecognition error:", err);
        }
    }

    start() {
        if (this.audioContext && this.audioContext.state === 'suspended') {
            try { this.audioContext.resume(); } catch (_) {}
        }

        if (this.recognition && !this.isListening) {
            try {
                this.currentTurn = new UserTurnContext();
                this.recognition.start();
                this.isListening = true;
            } catch (e) {
                if (e.name === 'InvalidStateError') this.isListening = true;
            }
        }
    }

    stop() {
        this.continuousVoiceMode = false;
        if (this.recognition && this.isListening) {
            try { this.recognition.stop(); } catch (_) {}
        }
        this.isListening = false;
    }
}

// Global SAVI Voice Facade
window.saviVoice = {
    dotNetRef: null,
    captureController: null,
    playbackController: null,

    isSupported: function () {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    },

    init: async function (dotNetHelper) {
        this.dotNetRef = dotNetHelper;
        this.playbackController = new AudioPlaybackController(this);
        this.captureController = new AudioCaptureController(this);

        const ok = await this.captureController.initAudioPipeline();
        this.setupKeyboardShortcuts();
        return ok;
    },

    setContinuousMode: function (enabled) {
        if (!this.captureController) return;
        this.captureController.continuousVoiceMode = enabled;
        if (enabled) {
            this.captureController.start();
        } else {
            this.captureController.stop();
            this.playbackController.duckAndStop(false);
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
            micActive: this.captureController?.isListening ?? false,
            speaking: this.playbackController?.isSpeaking ?? false,
            audioOutputMode: this.captureController?.audioOutputMode ?? 'speaker',
            hardwareAec: this.captureController?.hardwareAecEnabled,
            hardwareNs: this.captureController?.hardwareNsEnabled,
            hardwareAgc: this.captureController?.hardwareAgcEnabled,
            selfEchoSuppressedCount: this.captureController?.falseSelfDetectionCount ?? 0
        };
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
            this.captureController.start();
            return true;
        }
    },

    startListening: function () {
        if (this.captureController) {
            this.captureController.start();
            return true;
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
    }
};
