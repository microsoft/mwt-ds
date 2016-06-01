$(function () {
    $('.helptext-on-interaction').hover(function () {
        $('div[id^="div-sv-"]').hide();
        $('#div-' + this.id).fadeIn();
    });
})