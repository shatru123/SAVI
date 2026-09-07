// SAVI Voice Engine — Full-Duplex Real-Time Voice Conversation
// Supports Web Speech API (STT & TTS), Web Audio API VAD, Barge-in Interruption (<200ms), and Continuous Listening

window.saviVoice = {
    recognition: null,
    isListening: false,
    isSpeaking: false,
    continuousVoiceMode: false,
    dotNetRef: null,
    accumulatedTranscript: '',
    interimTranscript: '',
    hasDispatchedFinal: false,
    availableVoices: [],
    audioContext: null,
    analyserNode: null,
    micStream: null,
    animFrameId: null,
    chunkQueue: [],
    isChunkSpeaking: false,
    currentUtterance: null,
    lastAudioLevel: 0,
    vadSpeechCounter: 0,
    turnTimer: null,
    preRollRingBuffer: null,
    preRollWritePtr: 0,
    immediateTriggerRegex: /^(?:wait|stop|hold on|actually|no|pause|keep it short)\b/i,
    trailingConjunctionRegex: /\b(?:and|or|because|if|whether|with|that|for|like|so|also|plus|then|but)$/i,

    isSupported: function () {
        return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
    },

    detectBrowser: function () {
        const ua = navigator.userAgent;
        if (navigator.brave && typeof navigator.brave.isBrave === 'function') return 'Brave';
        if (/Edg/.test(ua)) return 'Edge';
        if (/Chrome/.test(ua) && /Google Inc/.test(navigator.vendor)) return 'Chrome';
        if (/^((?!chrome|android).)*safari/i.test(ua)) return 'Safari';
        if (/Firefox/.test(ua)) return 'Firefox';
        return 'Other';
    },

    init: function (dotNetHelper) {
        this.dotNetRef = dotNetHelper;
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.warn("SAVI: Web Speech API recognition not supported in this browser.");
            return false;
        }

        try {
            this.recognition = new SpeechRecognition();
            this.recognition.continuous = true;
            this.recognition.interimResults = true;
            this.recognition.maxAlternatives = 1;
            this.recognition.lang = navigator.language || 'en-US';

            const self = this;

            this.recognition.onstart = function () {
                self.isListening = true;
                // Only reset accumulated transcript if previous turn has already dispatched
                if (self.hasDispatchedFinal) {
                    self.accumulatedTranscript = '';
                    self.interimTranscript = '';
                    self.hasDispatchedFinal = false;
                }
                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                }
            };

            this.recognition.onresult = function (event) {
                // Barge-in: If user speaks while SAVI is currently speaking, immediately interrupt!
                if (self.isSpeaking) {
                    self.interruptSpeaking();
                }

                let currentInterim = '';
                let currentFinal = '';

                for (let i = event.resultIndex; i < event.results.length; ++i) {
                    const result = event.results[i];
                    if (result.isFinal) {
                        currentFinal += result[0].transcript + ' ';
                    } else {
                        currentInterim += result[0].transcript;
                    }
                }

                if (currentFinal) {
                    self.accumulatedTranscript += currentFinal;
                    self.hasDispatchedFinal = false;
                }
                self.interimTranscript = currentInterim;

                const fullText = (self.accumulatedTranscript + ' ' + self.interimTranscript).trim();

                if (currentInterim && self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 7); // 7 = DetectingSpeech
                    self.dotNetRef.invokeMethodAsync('OnSpeechInterim', fullText);
                }

                if (self.turnTimer) {
                    clearTimeout(self.turnTimer);
                    self.turnTimer = null;
                }

                const candidateText = (self.accumulatedTranscript + ' ' + self.interimTranscript).trim();
                if (candidateText) {
                    const trimmedUtterance = candidateText.replace(/[.,!?;:]+$/, '');
                    const endsWithConjunction = self.trailingConjunctionRegex.test(trimmedUtterance);
                    const isImmediate = self.immediateTriggerRegex.test(candidateText);

                    const dispatchFinal = function () {
                        if (self.turnTimer) {
                            clearTimeout(self.turnTimer);
                            self.turnTimer = null;
                        }
                        const finalUtterance = (self.accumulatedTranscript + ' ' + self.interimTranscript).trim();
                        self.accumulatedTranscript = '';
                        self.interimTranscript = '';
                        self.hasDispatchedFinal = true;
                        if (self.dotNetRef && finalUtterance) {
                            self.dotNetRef.invokeMethodAsync('OnSpeechRecognized', finalUtterance);
                        }
                    };

                    if (isImmediate && candidateText.split(/\s+/).length <= 4) {
                        // Immediate voice commands (stop, wait, keep it short) trigger with zero turn delay
                        dispatchFinal();
                    } else {
                        // Conjunction buffering gives user 1200ms to continue; standard pause is 700ms; interim fallback is 1000ms
                        const delayMs = endsWithConjunction ? 1200 : (currentFinal ? 700 : 1000);
                        self.turnTimer = setTimeout(dispatchFinal, delayMs);
                    }
                }
            };

            this.recognition.onerror = function (event) {
                console.warn("SAVI Speech Recognition Error:", event.error);

                if (event.error === 'no-speech') {
                    // Normal silence in continuous mode; do not terminate
                    return;
                }

                self.isListening = false;
                let friendlyMsg = null;
                switch (event.error) {
                    case 'not-allowed':
                    case 'service-not-allowed':
                        friendlyMsg = "Microphone access was denied. Please allow microphone permissions in your browser's address bar to speak to SAVI.";
                        break;
                    case 'audio-capture':
                        friendlyMsg = "No microphone hardware found. Please connect an audio input device.";
                        break;
                    case 'network':
                        const bName = self.detectBrowser();
                        if (bName === 'Brave') {
                            friendlyMsg = "Brave Browser blocks Google speech recognition by default. Enable 'Use Google services for speech recognition' in brave://settings/system, or use Safari for offline on-device speech!";
                        } else {
                            friendlyMsg = "Speech recognition network error: Google's speech service was unreachable. On macOS, Safari uses local on-device speech recognition without network dependencies.";
                        }
                        break;
                    case 'aborted':
                        break;
                    default:
                        friendlyMsg = "Voice input error: " + event.error;
                        break;
                }

                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 6); // 6 = Error
                    if (friendlyMsg) {
                        self.dotNetRef.invokeMethodAsync('OnVoiceError', friendlyMsg);
                    }
                }
            };

            this.recognition.onend = function () {
                self.isListening = false;
                const finalText = (self.accumulatedTranscript + ' ' + self.interimTranscript).trim();

                // If speech was spoken and not yet dispatched, dispatch it immediately!
                if (finalText && !self.hasDispatchedFinal) {
                    if (self.turnTimer) {
                        clearTimeout(self.turnTimer);
                        self.turnTimer = null;
                    }
                    self.accumulatedTranscript = '';
                    self.interimTranscript = '';
                    self.hasDispatchedFinal = true;
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnSpeechRecognized', finalText);
                    }
                }

                // If in continuous voice mode and not deliberately stopped, restart listening seamlessly
                if (self.continuousVoiceMode) {
                    try {
                        setTimeout(() => {
                            if (self.continuousVoiceMode && !self.isListening) {
                                self.recognition.start();
                                self.isListening = true;
                            }
                        }, 50);
                    } catch (_) {}
                } else {
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // 0 = Idle
                    }
                }
            };

            this.loadVoices();
            if (window.speechSynthesis) {
                window.speechSynthesis.onvoiceschanged = () => this.loadVoices();
            }

            this.setupKeyboardShortcuts();
            this.initAudioAnalyser();

            return true;
        } catch (e) {
            console.error("SAVI voice init error:", e);
            return false;
        }
    },

    loadVoices: function () {
        if (window.speechSynthesis) {
            this.availableVoices = window.speechSynthesis.getVoices();
        }
    },

    initAudioAnalyser: async function () {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return;
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;

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

            this.micStream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: true
                }
            });

            const source = this.audioContext.createMediaStreamSource(this.micStream);
            this.analyserNode = this.audioContext.createAnalyser();
            this.analyserNode.fftSize = 128;
            this.analyserNode.smoothingTimeConstant = 0.8;
            source.connect(this.analyserNode);

            // Web Audio circular ring buffer (retains ~300ms of pre-roll audio frames)
            try {
                if (this.audioContext.createScriptProcessor) {
                    const proc = this.audioContext.createScriptProcessor(1024, 1, 1);
                    const ringSamples = Math.round(this.audioContext.sampleRate * 0.3);
                    this.preRollRingBuffer = new Float32Array(ringSamples);
                    this.preRollWritePtr = 0;

                    const self = this;
                    proc.onaudioprocess = function (e) {
                        const input = e.inputBuffer.getChannelData(0);
                        const len = input.length;
                        for (let i = 0; i < len; i++) {
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
            } catch (_) {}

            this.startVisualizerLoop();
        } catch (err) {
            console.warn("SAVI: AudioContext/Analyser initialization notice:", err.message);
        }
    },

    startVisualizerLoop: function () {
        const self = this;
        const dataArray = new Uint8Array(this.analyserNode ? this.analyserNode.frequencyBinCount : 0);

        function renderFrame() {
            if (self.analyserNode) {
                self.analyserNode.getByteFrequencyData(dataArray);

                let sum = 0;
                for (let i = 0; i < dataArray.length; i++) {
                    sum += dataArray[i];
                }
                const avg = dataArray.length > 0 ? sum / dataArray.length : 0;
                const normalizedLevel = Math.min(1.0, Math.max(0.0, avg / 128.0));
                self.lastAudioLevel = normalizedLevel;

                // Update CSS variable --savi-audio-level for reactive waveform and glowing rings
                document.documentElement.style.setProperty('--savi-audio-level', normalizedLevel.toFixed(3));

                // Client-side VAD Barge-in: if user is speaking aloud during TTS, trigger immediate interrupt
                if (self.isSpeaking && normalizedLevel > 0.22) {
                    self.vadSpeechCounter++;
                    if (self.vadSpeechCounter >= 3) { // ~50ms of sustained speech above threshold
                        self.interruptSpeaking();
                        self.vadSpeechCounter = 0;
                    }
                } else {
                    self.vadSpeechCounter = 0;
                }
            }

            self.animFrameId = requestAnimationFrame(renderFrame);
        }

        renderFrame();
    },

    setContinuousMode: function (enabled) {
        this.continuousVoiceMode = enabled;
        if (enabled) {
            if (!this.isListening) {
                this.startListening();
            }
        } else {
            this.stopListening();
            this.stopSpeaking();
        }
    },

    toggleListening: async function () {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync(
                    'OnVoiceError',
                    'Web Speech recognition is not supported in this browser. Please use Chrome, Edge, or Safari.'
                );
            }
            return false;
        }

        if (!this.recognition) {
            const initialized = this.init(this.dotNetRef);
            if (!initialized) return false;
        }

        if (this.isListening) {
            this.stopListening();
            return false;
        } else {
            return await this.startListening();
        }
    },

    startListening: async function () {
        if (!this.recognition) return false;

        if (this.audioContext && this.audioContext.state === 'suspended') {
            try { await this.audioContext.resume(); } catch (_) {}
        }

        try {
            this.accumulatedTranscript = '';
            this.interimTranscript = '';
            this.hasDispatchedFinal = false;
            this.recognition.start();
            this.isListening = true;
            return true;
        } catch (e) {
            if (e.name === 'InvalidStateError') {
                this.isListening = true;
                return true;
            }
            console.error("SAVI recognition start error:", e);
            return false;
        }
    },

    stopListening: function () {
        this.continuousVoiceMode = false;
        if (this.recognition && this.isListening) {
            try {
                this.recognition.stop();
            } catch (_) {}
        }
        this.isListening = false;
        return false;
    },

    // Instant Barge-In / Interruption (<200ms)
    interruptSpeaking: function () {
        if (!this.isSpeaking && !window.speechSynthesis?.speaking) return;

        const stopStartTime = performance.now();
        console.log("SAVI: Instant barge-in triggered! Halting TTS playback.");
        this.isSpeaking = false;
        this.chunkQueue = [];
        this.isChunkSpeaking = false;

        // Instant acoustic ducking (<5ms)
        if (this.currentUtterance) {
            try { this.currentUtterance.volume = 0; } catch (_) {}
        }
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
        const audioStopLatencyMs = Math.round(performance.now() - stopStartTime);

        if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnUserInterrupted', audioStopLatencyMs);
                this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 8); // 8 = Interrupted
                setTimeout(() => {
                    if (this.dotNetRef && !this.isSpeaking) {
                        this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                    }
                }, 120);
            } catch (_) {}
        }
    },

    speak: function (text, rate = 1.0) {
        if (!window.speechSynthesis) return;

        try {
            // Cancel any prior speech
            if (this.currentUtterance) {
                try { this.currentUtterance.volume = 0; } catch (_) {}
            }
            window.speechSynthesis.cancel();
            this.chunkQueue = [];
            this.isChunkSpeaking = false;

            // Purge leftover transcripts from prior turns
            this.accumulatedTranscript = '';
            this.interimTranscript = '';
            this.hasDispatchedFinal = false;
            if (this.turnTimer) {
                clearTimeout(this.turnTimer);
                this.turnTimer = null;
            }

            // Strip code blocks for speech, replacing them with a natural cue
            let cleanText = text.replace(/```[\s\S]*?```/g, ' The code solution is displayed on screen. ');

            // Strip markdown tables, links, and bold
            cleanText = cleanText
                .replace(/\|[^\n]+\|\r?\n\|[-:\s|]+\|\r?\n(?:\|[^\n]+\|\r?\n?)*/g, ' The structured data is shown on your screen. ')
                .replace(/\*\*(.*?)\*\*/g, '$1')
                .replace(/\*(.*?)\*/g, '$1')
                .replace(/`{1,3}[^`]*`{1,3}/g, '')
                .replace(/\[(.*?)\]\([^)]+\)/g, '$1')
                .replace(/[#*_~>]/g, '')
                .replace(/\n+/g, '. ')
                .trim();

            if (!cleanText) return;

            // Split into natural sentences for streamed chunking
            const chunks = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];
            this.chunkQueue = chunks.map(c => c.trim()).filter(c => c.length > 0);

            if (this.chunkQueue.length === 0) return;

            this.isSpeaking = true;
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 5); // 5 = Speaking
            }

            this.playNextSpeechChunk(rate);
        } catch (e) {
            console.error("SAVI speak error:", e);
            this.isSpeaking = false;
            if (this.dotNetRef) this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0);
        }
    },

    playNextSpeechChunk: function (rate) {
        if (!this.isSpeaking) return;

        if (this.chunkQueue.length === 0) {
            this.isSpeaking = false;
            this.isChunkSpeaking = false;
            if (this.dotNetRef) {
                if (this.continuousVoiceMode) {
                    this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // 1 = Listening
                } else {
                    this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // 0 = Idle
                }
            }
            return;
        }

        const chunk = this.chunkQueue.shift();
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

        const self = this;
        this.currentUtterance = utterance;
        this.isChunkSpeaking = true;

        utterance.onend = function () {
            self.isChunkSpeaking = false;
            self.playNextSpeechChunk(rate);
        };

        utterance.onerror = function () {
            self.isChunkSpeaking = false;
            self.playNextSpeechChunk(rate);
        };

        window.speechSynthesis.speak(utterance);
    },

    stopSpeaking: function () {
        this.isSpeaking = false;
        this.chunkQueue = [];
        this.isChunkSpeaking = false;
        if (this.currentUtterance) {
            try { this.currentUtterance.volume = 0; } catch (_) {}
        }
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
        if (this.dotNetRef) {
            try { this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); } catch (_) {}
        }
    },

    setupKeyboardShortcuts: function () {
        const self = this;
        let spacePressed = false;

        window.addEventListener('keydown', function (e) {
            // Escape key: Immediate Silence / Interruption
            if (e.key === 'Escape') {
                if (self.isSpeaking) {
                    self.interruptSpeaking();
                } else if (self.isListening && !self.continuousVoiceMode) {
                    self.stopListening();
                }
            }

            // Space key: Push-To-Talk (when not focused in a text input)
            if (e.code === 'Space' && !spacePressed) {
                const tag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
                if (tag !== 'input' && tag !== 'textarea' && !document.activeElement?.isContentEditable) {
                    spacePressed = true;
                    if (self.isSpeaking) self.interruptSpeaking();
                    if (!self.isListening) self.startListening();
                }
            }
        });

        window.addEventListener('keyup', function (e) {
            if (e.code === 'Space' && spacePressed) {
                spacePressed = false;
                const tag = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
                if (tag !== 'input' && tag !== 'textarea') {
                    if (self.isListening && !self.continuousVoiceMode) {
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
                console.warn("SAVI autoSpeakGreeting attempt failed:", e);
            }
        };

        if (window.speechSynthesis) {
            if (window.speechSynthesis.getVoices().length === 0) {
                const prev = window.speechSynthesis.onvoiceschanged;
                window.speechSynthesis.onvoiceschanged = function () {
                    self.loadVoices();
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
