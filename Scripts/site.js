$(function () {
    $('.helptext-on-interaction').click(function () {
        $('div[id^="div-sv-"]').hide();
        $('#div-sv-' + this.id).fadeIn();
    });
})