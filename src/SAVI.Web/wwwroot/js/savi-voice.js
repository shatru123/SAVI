window.saviVoice = {
    recognition: null,
    isListening: false,
    dotNetRef: null,

    init: function (dotNetHelper) {
        this.dotNetRef = dotNetHelper;
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            console.warn("SAVI: Web Speech API recognition not supported in this browser.");
            return false;
        }

        this.recognition = new SpeechRecognition();
        this.recognition.continuous = false;
        this.recognition.interimResults = true;
        this.recognition.lang = 'en-US';

        const self = this;
        this.recognition.onstart = function () {
            self.isListening = true;
            if (self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 1); // Listening
            }
        };

        this.recognition.onresult = function (event) {
            let transcript = '';
            for (let i = event.resultIndex; i < event.results.length; ++i) {
                transcript += event.results[i][0].transcript;
            }
            if (event.results[0].isFinal && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnSpeechRecognized', transcript);
            }
        };

        this.recognition.onerror = function (event) {
            console.error("SAVI Speech Error:", event.error);
            self.isListening = false;
            if (self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 6); // Error
            }
        };

        this.recognition.onend = function () {
            self.isListening = false;
            if (self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // Idle
            }
        };

        return true;
    },

    toggleListening: function () {
        if (!this.recognition) return false;
        if (this.isListening) {
            this.recognition.stop();
            this.isListening = false;
        } else {
            try {
                this.recognition.start();
                this.isListening = true;
            } catch (e) {
                console.error("SAVI recognition start failed:", e);
            }
        }
        return this.isListening;
    },

    speak: function (text, rate = 1.0) {
        if (!window.speechSynthesis) return;
        window.speechSynthesis.cancel(); // Stop prior speech

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.rate = rate || 1.0;
        utterance.pitch = 1.0;

        const voices = window.speechSynthesis.getVoices();
        const preferredVoice = voices.find(v => v.lang.startsWith('en') && (v.name.includes('Natural') || v.name.includes('Google') || v.name.includes('Daniel') || v.name.includes('Samantha')));
        if (preferredVoice) {
            utterance.voice = preferredVoice;
        }

        const self = this;
        utterance.onstart = function () {
            if (self.dotNetRef) self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 5); // Speaking
        };
        utterance.onend = function () {
            if (self.dotNetRef) self.dotNetRef.invokeMethodAsync('OnVoiceStateChanged', 0); // Idle
        };

        window.speechSynthesis.speak(utterance);
    },

    stopSpeaking: function () {
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
    }
};
