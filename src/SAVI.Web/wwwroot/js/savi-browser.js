// SAVI browser compatibility boundary.
// Feature detection is authoritative; user-agent data is only a secondary
// diagnostic/workaround signal. Voice code should consume this object instead
// of branching on browser names throughout the application.
(function (window, navigator) {
    'use strict';

    class BrowserCapabilityService {
        constructor() {
            this.snapshot = this.detect();
            this.permissionState = 'unknown';
        }

        detect() {
            const ua = navigator.userAgent || '';
            const nav = navigator;
            const media = nav.mediaDevices;
            const supportedConstraints = media?.getSupportedConstraints?.() || {};
            const recognition = window.SpeechRecognition || window.webkitSpeechRecognition || null;
            const audioContext = window.AudioContext || window.webkitAudioContext || null;
            const hasTouch = ('ontouchstart' in window) || (nav.maxTouchPoints || 0) > 0;
            const uaLower = ua.toLowerCase();

            // Browser/OS names are diagnostic only. Runtime support below is
            // determined by actual feature presence.
            const isIOS = /iphone|ipad|ipod/.test(uaLower) ||
                (uaLower.includes('macintosh') && hasTouch);
            const isAndroid = uaLower.includes('android');
            const browser = /edg\//i.test(ua) ? 'Edge' :
                /firefox\//i.test(ua) ? 'Firefox' :
                /chrome\//i.test(ua) && !/edg\//i.test(ua) ? 'Chrome' :
                /safari\//i.test(ua) && !/chrome\//i.test(ua) ? 'Safari' : 'Unknown';
            const versionMatch = ua.match(/(?:edg|chrome|firefox|version|safari)\/(\d+(?:\.\d+)?)/i);

            return {
                browser,
                browserVersion: versionMatch ? versionMatch[1] : 'unknown',
                operatingSystem: isIOS ? 'iOS' : isAndroid ? 'Android' : /mac os|macintosh/i.test(ua) ? 'macOS' : /win/i.test(ua) ? 'Windows' : /linux/i.test(ua) ? 'Linux' : 'Unknown',
                device: hasTouch ? 'Touch device' : 'Desktop',
                isMobile: isIOS || isAndroid || hasTouch,
                isIOS,
                isAndroid,
                speechRecognition: typeof recognition === 'function',
                speechRecognitionImplementation: window.SpeechRecognition ? 'SpeechRecognition' : window.webkitSpeechRecognition ? 'webkitSpeechRecognition' : 'None',
                webkitSpeechRecognition: typeof window.webkitSpeechRecognition === 'function',
                speechSynthesis: typeof window.speechSynthesis !== 'undefined' && typeof window.SpeechSynthesisUtterance === 'function',
                getUserMedia: typeof media?.getUserMedia === 'function',
                audioContext: typeof audioContext === 'function',
                audioWorklet: typeof window.AudioWorkletNode === 'function' &&
                    (typeof AudioContext !== 'undefined' || typeof window.webkitAudioContext !== 'undefined'),
                mediaStream: typeof window.MediaStream === 'function',
                webAudio: typeof audioContext === 'function',
                echoCancellation: !!supportedConstraints.echoCancellation,
                noiseSuppression: !!supportedConstraints.noiseSuppression,
                autoGainControl: !!supportedConstraints.autoGainControl,
                permissionsApi: !!nav.permissions?.query,
                autoplay: 'autoplay' in document.createElement('audio'),
                audioOutputSelection: typeof media?.selectAudioOutput === 'function' ||
                    (typeof HTMLMediaElement !== 'undefined' && typeof HTMLMediaElement.prototype?.setSinkId === 'function'),
                viewport: {
                    width: window.innerWidth,
                    height: window.innerHeight,
                    visualHeight: window.visualViewport?.height || window.innerHeight,
                    orientation: window.screen?.orientation?.type || 'unknown'
                }
            };
        }

        async refreshPermissionState() {
            if (!navigator.permissions?.query) return this.permissionState;
            try {
                const permission = await navigator.permissions.query({ name: 'microphone' });
                this.permissionState = permission.state;
                permission.onchange = () => {
                    this.permissionState = permission.state;
                    this.emitChange();
                };
            } catch (_) {
                this.permissionState = 'unknown';
            }
            this.emitChange();
            return this.permissionState;
        }

        emitChange() {
            window.dispatchEvent(new CustomEvent('savi:capabilitieschanged', { detail: this.diagnostics() }));
        }

        diagnostics(extra = {}) {
            return {
                ...this.snapshot,
                microphonePermission: this.permissionState,
                ...extra
            };
        }
    }

    class BrowserAudioAdapter {
        constructor(capabilities) {
            this.capabilities = capabilities;
            this.stream = null;
            this.context = null;
            this.source = null;
            this.analyser = null;
            this.processor = null;
            this.workletNode = null;
            this.audioWorkletActive = false;
            this.settings = {};
            this.lastError = null;
            this.trackEnded = false;
        }

        async acquire(constraints = {}) {
            if (!this.capabilities.snapshot.getUserMedia) {
                throw new DOMException('Microphone access is unavailable in this browser.', 'NotSupportedError');
            }
            const requested = {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true,
                ...constraints
            };
            try {
                if (!this.stream || !this.stream.active) {
                    try {
                        this.stream = await navigator.mediaDevices.getUserMedia({ audio: requested });
                    } catch (error) {
                        // Some Safari/WebView implementations reject a constraint
                        // instead of ignoring it. Retry with the portable minimum.
                        if (error?.name !== 'OverconstrainedError' && error?.name !== 'TypeError') throw error;
                        this.stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                    }
                }
                const track = this.stream.getAudioTracks?.()[0];
                this.trackEnded = false;
                track?.addEventListener?.('ended', () => {
                    this.trackEnded = true;
                    this.lastError = new DOMException('Microphone track ended.', 'AbortError');
                }, { once: true });
                this.settings = track?.getSettings?.() || {};
                await this.ensureContext();
                return this.diagnostics();
            } catch (error) {
                this.lastError = error;
                throw error;
            }
        }

        async ensureContext() {
            const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextCtor) return null;
            if (!this.context) this.context = new AudioContextCtor();
            if (this.context.state === 'suspended') await this.context.resume();
            return this.context;
        }

        async connectAnalyser(onFrame) {
            if (!this.stream) return false;
            const context = await this.ensureContext();
            if (!context) return false;
            this.source ??= context.createMediaStreamSource(this.stream);
            this.analyser ??= context.createAnalyser();
            this.analyser.fftSize = 256;
            this.analyser.smoothingTimeConstant = 0.7;
            this.source.connect(this.analyser);

            if (context.audioWorklet && typeof AudioWorkletNode === 'function') {
                try {
                    const code = `class SaviAudioProcessor extends AudioWorkletProcessor { process(inputs) { const input = inputs[0]; if (input?.[0]) this.port.postMessage(input[0]); return true; } } registerProcessor('savi-audio-processor', SaviAudioProcessor);`;
                    const url = URL.createObjectURL(new Blob([code], { type: 'application/javascript' }));
                    await context.audioWorklet.addModule(url);
                    URL.revokeObjectURL(url);
                    this.workletNode = new AudioWorkletNode(context, 'savi-audio-processor');
                    this.workletNode.port.onmessage = event => onFrame?.(event.data);
                    this.source.connect(this.workletNode);
                    this.audioWorkletActive = true;
                    return true;
                } catch (_) {
                    this.audioWorkletActive = false;
                }
            }

            if (context.createScriptProcessor) {
                this.processor = context.createScriptProcessor(1024, 1, 1);
                this.processor.onaudioprocess = event => onFrame?.(event.inputBuffer.getChannelData(0));
                const silentGain = context.createGain();
                silentGain.gain.value = 0;
                this.source.connect(this.processor);
                this.processor.connect(silentGain);
                silentGain.connect(context.destination);
                return true;
            }
            return false;
        }

        diagnostics() {
            const status = key => {
                if (!this.capabilities.snapshot[key]) return 'Unsupported';
                if (this.settings[key] === undefined) return 'Unknown';
                return this.settings[key] ? 'Enabled' : 'Disabled';
            };
            return {
                audioContextState: this.context?.state || 'Not initialized',
                sampleRate: this.settings.sampleRate || this.context?.sampleRate || null,
                aec: status('echoCancellation'),
                ns: status('noiseSuppression'),
                agc: status('autoGainControl'),
                audioWorklet: this.audioWorkletActive ? 'Active' : this.capabilities.snapshot.audioWorklet ? 'Fallback/Inactive' : 'Unsupported',
                streamActive: !!this.stream?.active,
                trackReadyState: this.stream?.getAudioTracks?.()[0]?.readyState || 'none',
                trackEnded: this.trackEnded,
                lastError: this.lastError?.name || null
            };
        }

        release() {
            this.processor?.disconnect?.();
            this.workletNode?.disconnect?.();
            this.source?.disconnect?.();
            this.stream?.getTracks?.().forEach(track => track.stop());
            this.stream = null;
            this.source = null;
            this.analyser = null;
            this.processor = null;
            this.workletNode = null;
            this.audioWorkletActive = false;
            this.trackEnded = false;
            // The context is intentionally kept alive for the session; close it
            // only when the user explicitly exits Voice Mode.
        }

        async close() {
            this.release();
            if (this.context && this.context.state !== 'closed') await this.context.close();
            this.context = null;
        }
    }

    class VoiceSessionController {
        constructor(capabilities) {
            this.capabilities = capabilities;
            this.state = 'IDLE';
            this.sessionId = null;
            this.conversationId = null;
            this.turnId = null;
            this.generationId = null;
            this.generationCounter = 0;
            this.restartCount = 0;
            this.recognitionErrors = [];
            this.lastSttEvent = null;
            this.lastAudioEvent = null;
            this.startedAt = 0;
            this.intentionalStop = false;
            this.listeners = new Set();
        }

        on(listener) { this.listeners.add(listener); return () => this.listeners.delete(listener); }

        transition(next, detail = {}) {
            this.state = next;
            this.listeners.forEach(listener => listener(next, detail));
        }

        ensureSession() {
            this.sessionId ??= 'voice_' + Math.random().toString(36).slice(2) + Date.now();
            this.startedAt ||= Date.now();
            return this.sessionId;
        }

        start(conversationId) {
            this.intentionalStop = false;
            this.ensureSession();
            this.conversationId = conversationId || this.conversationId;
            this.startedAt ||= Date.now();
            this.transition('REQUESTING_PERMISSION', { conversationId: this.conversationId });
        }

        beginTurn(turnId) {
            this.turnId = turnId;
            this.generationId = 'gen_' + Math.random().toString(36).slice(2) + Date.now();
            this.transition('USER_SPEAKING', { turnId });
            return this.generationId;
        }

        nextGeneration(turnId = this.turnId) {
            this.ensureSession();
            this.turnId = turnId;
            this.generationId = `${this.sessionId || 'session'}:g${++this.generationCounter}`;
            return this.generationId;
        }

        processing(turnId = this.turnId) { this.turnId = turnId; this.transition('PROCESSING', { turnId }); }
        speaking(turnId = this.turnId) { this.turnId = turnId; this.transition('SAVI_SPEAKING', { turnId }); }
        userSpeaking(turnId = this.turnId) { this.turnId = turnId; this.transition('USER_SPEAKING', { turnId }); }
        listening() { this.transition('LISTENING', { turnId: this.turnId }); }
        interrupted(reason = 'user') { this.transition('INTERRUPTED', { turnId: this.turnId, reason }); }
        stop() { this.intentionalStop = true; this.transition('IDLE'); }
        recordStt(event, detail = {}) { this.lastSttEvent = { event, at: new Date().toISOString(), ...detail }; }
        recordAudio(event, detail = {}) { this.lastAudioEvent = { event, at: new Date().toISOString(), ...detail }; }

        diagnostics() {
            return {
                sessionId: this.sessionId,
                conversationId: this.conversationId,
                state: this.state,
                turnId: this.turnId,
                generationId: this.generationId,
                restartCount: this.restartCount,
                generationCounter: this.generationCounter,
                recognitionErrors: this.recognitionErrors.slice(-8),
                lastSttEvent: this.lastSttEvent,
                lastAudioEvent: this.lastAudioEvent,
                startedAt: this.startedAt ? new Date(this.startedAt).toISOString() : null
            };
        }
    }

    window.saviBrowser = {
        capabilities: new BrowserCapabilityService(),
        audio: null,
        voiceSession: null,
        init() {
            this.audio ??= new BrowserAudioAdapter(this.capabilities);
            this.voiceSession ??= new VoiceSessionController(this.capabilities);
            this.capabilities.refreshPermissionState();
            return this;
        },
        diagnostics() {
            this.init();
            return {
                ...this.capabilities.diagnostics(),
                ...this.audio.diagnostics(),
                ...this.voiceSession.diagnostics()
            };
        },
        async copyText(text) {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(String(text ?? ''));
                return true;
            }
            return false;
        },
        scrollToBottom(elementId) {
            const el = document.getElementById(elementId);
            if (el) {
                el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
            }
        },
        isScrolledNearBottom(elementId, threshold = 80) {
            const el = document.getElementById(elementId);
            if (!el) return true;
            return (el.scrollHeight - el.scrollTop - el.clientHeight) <= threshold;
        }
    }.init();
})(window, navigator);
