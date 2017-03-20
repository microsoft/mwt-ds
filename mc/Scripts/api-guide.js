$(function () {
    var eventId;

    // Set up radio button listeners to update the HTTP request examples
    // Context radio buttons
    $("input[name='optradio']").change(function () {
        if ($("input[name='optradio']:checked").val() == 'Seattle')
            $("#decisionRequestBody").text('{"Location":"Seattle"}');
        else {
            $("#decisionRequestBody").text('{"Location":"NYC"}');
        }
    });

    // Reward radio buttons
    $("input[name='rewardRadio']").change(function () {
        // TODO: show reward as body
        if ($("input[name='rewardRadio']:checked").val() == 'Click') {
            $("#rewardRequest").text("https://{hostname}/API/Reward?eventId=" + eventId);
            $("#rewardRequestBody").text("1");
        }
        else {
            $("#rewardRequest").text("https://{hostname}/API/Reward?eventId=" + eventId);
            $("#rewardRequestBody").text("0");
        }

    });

    $("#decision").hide();

    // Make decision request and show the response and reward sections
    $("#getDecision").click(function () {
        // Get Decision

        $.ajax({
            method: "POST",
            url: "/API/Policy",
            data: $("#decisionRequestBody").text(),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            cache: false,
            headers: {
                "auth": $("#userToken").val()
            },
        })
        .done(function (data) {
            decision = data.Action;
            eventId = data.EventId;

            $("#decisionResponse").text(JSON.stringify(data));

            showDecision(decision);
        })
        .fail(function (jqXHR, textStatus, errorThrown) {
            $("#status").text("Error: " + textStatus + "  " + errorThrown);
        });
    });


    function showDecision(decision) {

        // Show Decision
        $("#decision").show();
        switch (decision) {
            case 1:
                $("#articleImage").attr("src", "https://upload.wikimedia.org/wikipedia/commons/1/1c/Artificial.intelligence.jpg");
                $("#articleImage").width(260.7);
                $("#articleImage").height(200);
                $("#articleImage").css("margin-left", "-10px");
                $("#articleImage").css("margin-bottom", "0px");
                $("#articleImage").css("margin-top", "0px");
                $("#story").text("What counts as artificially intelligent? AI explained");
                break;
            case 2:
                $("#articleImage").attr("src", "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Marriner_S._Eccles_Federal_Reserve_Board_Building.jpg/1280px-Marriner_S._Eccles_Federal_Reserve_Board_Building.jpg");
                $("#articleImage").width(360);
                $("#articleImage").height(200);
                $("#articleImage").css("margin-left", "-60px");
                $("#articleImage").css("margin-bottom", "0px");
                $("#articleImage").css("margin-top", "0px");
                $("#story").text("Why the Fed isn't out of options");
                break;
            default:
                $("#headline").text("Something went wrong!");
        }

        // Update reward request
        // TODO: reward=1 needs to be passed as data
        $("#rewardRequest").text("https://{hostname}/API/Reward?eventId=" + eventId);
        $("#rewardRequestBody").text("1");

        // Disable decide button
        $("#getDecision").prop("disabled", true);

        $('html, body').animate({ scrollTop: $(document).height() - $(window).height() }, 1000);
    }

    // Submit reward
    $("#submitReward").click(function () {
        $.ajax({
            method: "POST",
            url: "/API/Reward?eventId=" + eventId,
            data: $("#rewardRequestBody").text(),
            headers: {
                "auth": $("#userToken").val()
            },
        })
        .done(function (data) {
            $("#training").show();
            $('html, body').animate({ scrollTop: $(document).height() - $(window).height() }, 1000);
        });
        // TODO: handle failure
    });

    $("#resetButton").click(function () {
        // Hide decision and enable decision button
        $("#getDecision").prop("disabled", false);
        $("#storyClick").prop("disabled", false);
        $("#storyNoClick").prop("disabled", false);
        $("#decision").hide();
        $("#training").hide();

        $("input:radio").prop("checked", false);
        $("#contextDefaultRadio").prop("checked", true);
        $("#rewardDefaultRadio").prop("checked", true);
        $("#decisionRequestBody").text("{\"Location\": \"Seattle\"}");
    });
});
