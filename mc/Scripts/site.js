$(function () {
    AddAntiForgeryToken = function (data) {
        data.__RequestVerificationToken = $('input[name=__RequestVerificationToken]').val();
        return data;
    };

    $('.helptext-on-interaction').click(function () {
        $('div[id^="div-sv-"]').hide();
        $('#div-sv-' + this.id).fadeIn();
    });

    $('.ds-form').on('click', '.resetter', function () {
        var reset = confirm("Are you sure you want to reset?");
        if (reset == true) {
            $this = $(this);
            var relatedInputId = $this.attr("name");
            var relatedInputValue = $("#" + relatedInputId).val();
            $.ajax({
                url: '/Home/Reset/',
                type: "POST",
                data: AddAntiForgeryToken({
                    inputId: relatedInputId,
                    inputValue: relatedInputValue
                }),
                dataType: "html",
                cache: false,
                success: function (data) {
                    alert("Reset completed successfully.");
                },
                error: function (jqXHR, textStatus, errorThrown) {
                    alert("Reset failed due to internal server error, check Application Insights for more details.");
                }
            });
        };
    });
})