// SAVI Voice Engine — Web Speech API Integration
// Supports SpeechRecognition (STT) and SpeechSynthesis (TTS)

window.saviVoice = {
    recognition: null,
    isListening: false,
    dotNetRef: null,
    accumulatedTranscript: '',
    interimTranscript: '',
    hasDispatchedFinal: false,
    availableVoices: [],

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
            this.recognition.continuous = false;
            this.recognition.interimResults = true;
            this.recognition.maxAlternatives = 1;
            this.recognition.lang = navigator.language || 'en-US';

            const self = this;

            this.recognition.onstart = function () {
                self.isListening = true;
                self.accumulatedTranscript = '';
                self.interimTranscript = '';
                self.hasDispatchedFinal = false;
                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // Listening
                }
            };

            this.recognition.onresult = function (event) {
                let currentInterim = '';
                for (let i = event.resultIndex; i < event.results.length; ++i) {
                    const result = event.results[i];
                    if (result.isFinal) {
                        self.accumulatedTranscript += result[0].transcript + ' ';
                    } else {
                        currentInterim += result[0].transcript;
                    }
                }
                self.interimTranscript = currentInterim;

                const fullText = (self.accumulatedTranscript + self.interimTranscript).trim();
                if (fullText && self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnSpeechInterim', fullText);
                }
            };

            this.recognition.onerror = function (event) {
                console.warn("SAVI Speech Recognition Error:", event.error);
                self.isListening = false;

                let friendlyMsg = null;
                switch (event.error) {
                    case 'not-allowed':
                    case 'service-not-allowed':
                        friendlyMsg = "Microphone access was denied. Please click the camera/mic icon in your browser's address bar to allow microphone access.";
                        break;
                    case 'no-speech':
                        friendlyMsg = "No speech was detected. Please try speaking closer to your microphone.";
                        break;
                    case 'audio-capture':
                        friendlyMsg = "No microphone hardware found. Please ensure a microphone is connected and configured in system settings.";
                        break;
                    case 'network':
                        const bName = self.detectBrowser();
                        if (bName === 'Brave') {
                            friendlyMsg = "Brave Browser blocks Google speech recognition by default. To fix: go to brave://settings/system, enable 'Use Google services for speech recognition', and refresh. Or open SAVI in Safari for 100% offline on-device speech!";
                        } else {
                            friendlyMsg = "Speech recognition network error: Google's cloud speech service was unreachable (blocked by VPN, firewall, or ad-blocker). Tip on macOS: Open SAVI in Safari (http://localhost:5212), which uses Apple's local on-device dictation without any cloud dependencies!";
                        }
                        break;
                    case 'aborted':
                        // User cancelled or stopped explicitly; no warning needed
                        break;
                    default:
                        friendlyMsg = "Voice input error: " + event.error;
                        break;
                }

                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 6); // Error
                    if (friendlyMsg) {
                        self.dotNetRef.invokeMethodAsync('OnVoiceError', friendlyMsg);
                    }
                }
            };

            this.recognition.onend = function () {
                self.isListening = false;
                const finalText = (self.accumulatedTranscript + self.interimTranscript).trim();

                if (self.dotNetRef) {
                    self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // Idle
                    if (finalText && !self.hasDispatchedFinal) {
                        self.hasDispatchedFinal = true;
                        self.dotNetRef.invokeMethodAsync('OnSpeechRecognized', finalText);
                    }
                }
            };

            this.loadVoices();
            if (window.speechSynthesis) {
                window.speechSynthesis.onvoiceschanged = () => this.loadVoices();
            }

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

    toggleListening: async function () {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync(
                    'OnVoiceError',
                    'Web Speech recognition is not supported in this browser. Please use Google Chrome, Microsoft Edge, or Apple Safari for voice input.'
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

        // Proactively request / test microphone permission if supported
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            try {
                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                // Clean up permission test tracks immediately
                stream.getTracks().forEach(t => t.stop());
            } catch (err) {
                console.warn("Microphone permission denied:", err);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync(
                        'OnVoiceError',
                        "Microphone access was denied. Please allow microphone permissions in your browser's address bar to speak to SAVI."
                    );
                }
                return false;
            }
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
                // Recognition already started or active
                this.isListening = true;
                return true;
            }
            console.error("SAVI recognition start failed:", e);
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnVoiceError', "Could not start voice recognition: " + e.message);
            }
            return false;
        }
    },

    stopListening: function () {
        if (this.recognition && this.isListening) {
            try {
                this.recognition.stop();
            } catch (e) {
                console.warn("SAVI recognition stop exception:", e);
            }
        }
        this.isListening = false;
        return false;
    },

    speak: function (text, rate = 1.0) {
        if (!window.speechSynthesis) return;
        try {
            window.speechSynthesis.cancel(); // Stop prior speech

            // Strip code blocks for speech, replacing them with a natural cue
            let cleanText = text.replace(/```[\s\S]*?```/g, ' The code solution is provided below. ');

            // Strip markdown formatting, symbols, and links
            cleanText = cleanText
                .replace(/\*\*(.*?)\*\*/g, '$1')
                .replace(/\*(.*?)\*/g, '$1')
                .replace(/`{1,3}[^`]*`{1,3}/g, '')
                .replace(/\[(.*?)\]\([^)]+\)/g, '$1')
                .replace(/[#*_~>]/g, '')
                .replace(/\n+/g, '. ')
                .trim();

            if (!cleanText) return;

            // Split into sentences / manageable chunks to prevent Chrome 15s cutoff bug
            const chunks = cleanText.match(/[^.!?]+[.!?]+|[^.!?]+$/g) || [cleanText];

            if (!this.availableVoices || this.availableVoices.length === 0) {
                this.loadVoices();
            }

            const preferredVoice = this.availableVoices.find(v =>
                v.lang.startsWith('en') &&
                (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Daniel') || v.name.includes('Samantha') || v.name.includes('Karen') || v.name.includes('Siri'))
            ) || this.availableVoices.find(v => v.lang.startsWith('en'));

            const self = this;
            if (self.dotNetRef) self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 5); // Speaking

            let chunkIndex = 0;

            function speakNextChunk() {
                if (chunkIndex >= chunks.length) {
                    if (self.dotNetRef) self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // Idle
                    return;
                }

                const chunk = chunks[chunkIndex++].trim();
                if (!chunk) {
                    speakNextChunk();
                    return;
                }

                const utterance = new SpeechSynthesisUtterance(chunk);
                utterance.rate = rate || 1.0;
                utterance.pitch = 1.0;
                if (preferredVoice) utterance.voice = preferredVoice;

                utterance.onend = function () {
                    speakNextChunk();
                };

                utterance.onerror = function () {
                    if (self.dotNetRef) self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0);
                };

                window.speechSynthesis.speak(utterance);
            }

            speakNextChunk();
        } catch (e) {
            console.error("SAVI speak error:", e);
            if (this.dotNetRef) this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0);
        }
    },

    stopSpeaking: function () {
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
        if (this.dotNetRef) {
            try { this.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); } catch (_) {}
        }
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

        // If voices aren't loaded yet, wait for onvoiceschanged
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

        // Browser Autoplay Policy unlocker: attaches a 1-time gesture listener
        // so that if autoplay is paused by the browser, the very first click/tap triggers speech immediately
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
