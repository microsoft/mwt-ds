$(function () {
    // create the editor
    var container = document.getElementById("jsoneditor");
    var options = {
        mode: 'code',
        modes: ['code', 'tree'], // allowed modes
    };
    var editor = new JSONEditor(container, options);

    // set json
    var json = {
        "Age": 25,
        "Location": "New York",
        _multi: [
            {"a":1},
            { "a": 2 }
        ]
    };
    editor.set(json);

    var lastJson = "";
    setInterval(function () {
        var newJson = editor.getText();

        if (lastJson == newJson)
            return;

        lastJson = newJson;

        $.ajax({
            url: "/API/Validate/",
            headers: { 'auth': $("#userToken").val() },
            type: "POST",
            data: newJson,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            cache: false,
            success: function (data) {
                $("#vwstr").html("<pre>" + data.VWExample + "</pre>");
            },
            error: function (jqXHR, textStatus, errorThrown) {
                $("#vwstr").html("Unable to validate due to error = " + textStatus);
            }
        });
    }, 500);

    function SubmitInteraction()
    {
        // Get Decision
        $.ajax({
            method: "POST",
            url: "/API/Ranker",
            data: editor.getText(),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            headers: {
                'auth': $("#userToken").val()
            },
        })
        .done(function (data) {
            $("#status").text("Success");

            $("#EventId").text(data.EventId);
        })
        .fail(function (jqXHR, textStatus, errorThrown) {
            $("#status").text("Error posting ineraction: " + textStatus + "  " + errorThrown);
        });
    }

    function SubmitObservation() {
        $.ajax({
            method: "POST",
            url: "/API/Reward/" + $("#EventId").text(),
            data: $("#reward").val(),
            headers: {
                'auth': $("#userToken").val()
            },
        })
        .done(function (data) {
            $("#status").text("Success");

            $("#EventId").text(data.EventId);
            $("#TimeStamp").text(data.TimeStamp);
        })
        .fail(function (jqXHR, textStatus, errorThrown) {
            $("#status").text("Error sending reward: " + textStatus + "  " + errorThrown);
        });
    }

    $('#submit-interaction').click(SubmitInteraction);
    $('#submit-observation').click(SubmitObservation)
})