const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync(
    require('node:path').join(__dirname, '../../src/SAVI.Web/wwwroot/js/savi-browser.js'),
    'utf8');

function loadBrowser({ recognition = false, webkitRecognition = false, mediaDevices = null } = {}) {
    const events = [];
    const document = {
        createElement: () => ({ autoplay: false }),
    };
    const window = {
        SpeechRecognition: recognition ? function SpeechRecognition() {} : undefined,
        webkitSpeechRecognition: webkitRecognition ? function SpeechRecognition() {} : undefined,
        AudioContext: undefined,
        speechSynthesis: undefined,
        SpeechSynthesisUtterance: undefined,
        MediaStream: undefined,
        addEventListener: () => {},
        dispatchEvent: event => events.push(event),
    };
    const navigator = {
        userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1',
        maxTouchPoints: 5,
        mediaDevices,
        permissions: undefined,
        language: 'en-US',
    };
    const context = {
        window,
        navigator,
        document,
        CustomEvent: class CustomEvent { constructor(type, init) { this.type = type; this.detail = init?.detail; } },
        DOMException,
        AudioContext: undefined,
        console,
    };
    vm.runInNewContext(source, context, { filename: 'savi-browser.js' });
    return { browser: window.saviBrowser, events };
}

test('capability detection uses actual APIs instead of browser labels', () => {
    const unsupported = loadBrowser();
    assert.equal(unsupported.browser.capabilities.snapshot.speechRecognition, false);
    assert.equal(unsupported.browser.capabilities.snapshot.getUserMedia, false);

    const supported = loadBrowser({
        webkitRecognition: true,
        mediaDevices: {
            getUserMedia: async () => ({ active: true, getAudioTracks: () => [] }),
            getSupportedConstraints: () => ({ echoCancellation: true, noiseSuppression: true }),
        },
    });
    assert.equal(supported.browser.capabilities.snapshot.speechRecognition, true);
    assert.equal(supported.browser.capabilities.snapshot.speechRecognitionImplementation, 'webkitSpeechRecognition');
    assert.equal(supported.browser.capabilities.snapshot.getUserMedia, true);
    assert.equal(supported.browser.capabilities.snapshot.echoCancellation, true);
});

test('microphone acquisition retries with portable constraints after a browser rejects optional constraints', async () => {
    const calls = [];
    const { browser } = loadBrowser({
        mediaDevices: {
            getSupportedConstraints: () => ({}),
            getUserMedia: async constraints => {
                calls.push(constraints);
                if (calls.length === 1) throw new DOMException('unsupported', 'OverconstrainedError');
                return { active: true, getAudioTracks: () => [] };
            },
        },
    });

    await browser.audio.acquire();
    assert.equal(calls.length, 2);
    assert.equal(calls[1].audio, true);
});

test('voice session generations are monotonic and remain tied to the session', () => {
    const { browser } = loadBrowser();
    browser.voiceSession.start('conversation-1');
    const first = browser.voiceSession.nextGeneration('turn-1');
    const second = browser.voiceSession.nextGeneration('turn-2');

    assert.notEqual(first, second);
    assert.match(first, /:g1$/);
    assert.match(second, /:g2$/);
    assert.equal(browser.voiceSession.conversationId, 'conversation-1');
    assert.equal(browser.voiceSession.diagnostics().generationCounter, 2);
});
