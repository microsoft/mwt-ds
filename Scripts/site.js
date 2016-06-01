$(function () {
    $('.helptext-on-interaction').click(function () {
        $('div[id^="div-sv-"]').hide();
        $('#div-' + this.id).fadeIn();
    });
})