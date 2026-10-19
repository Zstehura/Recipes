// When the page is hidden (switching apps on a phone, or changing tabs), commit whatever is
// being typed. Blazor form fields send their value on "change", which fires on blur, so blurring
// the focused field sends the latest text to the server, where it's saved as a draft, before
// the browser suspends the page. Best effort: if the browser suspends the page first, the
// draft holds everything up to the previous field.
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden' && document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
});
