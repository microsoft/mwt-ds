$.ajaxSetup({ cache: false });

var locs = ["Seattle", "New York"];
var genders = ["Male", "Female"];
var industries = ["Tech", "Law"];
var ages = ["Young", "Old"];
var persons = [
    { name: "John" }, { name: "Sid" }, { name: "Markus" }, { name: "Louie" },
    { name: "Dan" }, { name: "Gal" }, { name: "Alex" }, { name: "Alekh" },
    { name: "Jennifer" }, { name: "Sarah" }, { name: "Irene" }, { name: "Lauren" },
    { name: "Hannah" }, { name: "Danah" }, { name: "Mary" }, { name: "Nancy" }
];
for (var p = 0; p < persons.length; p++) {
    persons[p]['gender'] = genders[(p & 8) >> 3];
    persons[p]['location'] = locs[(p & 4) >> 2];
    persons[p]['industry'] = industries[(p & 2) >> 1];
    persons[p]['age'] = ages[p & 1];
}
var actions = [
    {
        text: "What counts as artificially intelligent? AI explained",
        image: "https://upload.wikimedia.org/wikipedia/commons/2/28/Artificial-intelligence-elon-musk-hawking.jpg",
        imgStyle: "width: 260.7px; height: 200px; margin-left:-10px"
    },
    {
        text: "Why the Federal Reserve isn't out of options yet",
        image: "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Marriner_S._Eccles_Federal_Reserve_Board_Building.jpg/1280px-Marriner_S._Eccles_Federal_Reserve_Board_Building.jpg",
        imgStyle: "width: 360px; height: 200px; overflow:hidden; margin-left:-60px"
    }
];

var secondsLeft = 10;
var locationIndex = 0;
var eventId;

var context_richness = 1;

function shuffle(a) {
    var j, x, i;
    for (i = a.length; i; i -= 1) {
        j = Math.floor(Math.random() * i);
        x = a[i - 1];
        a[i - 1] = a[j];
        a[j] = x;
    }
}

function chooseAction() {
    locationIndex = (locationIndex + 1) % persons.length;
    $('#userContext-name').text(persons[locationIndex].name);
    $('#userContext-age').text('Age: ' + persons[locationIndex].age);
    $('#userContext-gender').text('Gender: ' + persons[locationIndex].gender);
    $('#userContext-location').text('Location: ' + persons[locationIndex].location);
    $('#userContext-industry').text('Industry: ' + persons[locationIndex].industry);

    secondsLeft = 10;

    var context = {};
    switch (context_richness) {
        case 4:
            context["Industry"] = persons[locationIndex].industry;
        case 3:
            context["Location"] = persons[locationIndex].location;
        case 2:
            context["Gender"] = persons[locationIndex].gender;
        case 1:
            context["Age"] = persons[locationIndex].age;
            break;
    }

    // Get Decision
    $.ajax({
        method: "POST",
        url: "/API/Policy",
        data: JSON.stringify(context),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        headers: {
            "auth": $("#userToken").val()
        },
    })
    .done(function (data) {
        eventId = data.EventId;
        modelTime = data.ModelTime;
        $("#article").attr("src", actions[data.Action - 1].image);
        $("#article").attr("style", actions[data.Action - 1].imgStyle);
        $("#articleText").text(actions[data.Action - 1].text);
        $("#statusModel").text('Latest model: ' + new Date(parseInt(modelTime.substr(6))));
    })
    .fail(function (jqXHR, textStatus, errorThrown) {
        $("#status").text("Error: " + textStatus + "  " + errorThrown);
    });
};

function reportReward(reward) {
    var thisEventId = eventId;
    if ($('#thumbUp').hasClass('disabled') || $('#thumbDown').hasClass('disabled')) {
        return;
    }
    $('#thumbUp').addClass('disabled');
    $('#thumbDown').addClass('disabled');
    $.ajax({
        method: "POST",
        url: "/API/Reward/?eventId=" + eventId,
        data: "" + reward,
        headers: {
            "auth": $("#userToken").val()
        },
    })
    .done(function (data) {
        $("#statusReward").text("Success. Event ID: " + thisEventId + " and reward: " + reward);
        chooseAction();
    })
    .fail(function (jqXHR, textStatus, errorThrown) {
        $("#status").text("Error: " + textStatus + "  " + errorThrown);
    })
    .always(function () {
        $('#thumbUp').removeClass('disabled');
        $('#thumbDown').removeClass('disabled');
    });
}

function resetModel() {
    $.ajax({
        method: "POST",
        url: "/API/reset",
        headers: {
            "auth": $('#trainerToken').val()
        },
    })
    .done(function (data) {
        $("#statusModel").text("Successfully reset.");
    })
    .fail(function (jqXHR, textStatus, errorThrown) {
        $("#statusModel").text("Error: " + textStatus + "  " + errorThrown);
    });
}

$('#context-richness').on('change', function () {
    $('#userContext-age').hide();
    $('#userContext-gender').hide();
    $('#userContext-location').hide();
    $('#userContext-industry').hide();
    context_richness = parseInt(this.value);
    switch (context_richness) {
        case 4:
            $('#userContext-industry').show();
        case 3:
            $('#userContext-location').show();
        case 2:
            $('#userContext-gender').show();
        case 1:
            $('#userContext-age').show();
            break;
    }
});

shuffle(persons);
chooseAction();

$(document).keydown(function (e) {
    switch (e.which) {
        case 49: // '1' // 38: // up
            reportReward(1);
            break;
        case 48: // '0' // 40: // down
            reportReward(0);
            break;
    }
});

setInterval(function () {
    $.ajax({
        method: "GET",
        url: "/API/trainerStatus"
    })
    .done(function (data) {
        /*
        var str = "";
        for (var property in data) {
            if (data.hasOwnProperty(property)) {
                str += property.replace("_", " ") + ": " + data[property] + " | ";
            }
        }
        */
        $("#statusTrainer").text("Trainer OK. Total learned examples: " + data['Stage2_Learn_Total']);
    })
    .fail(function (jqXHR, textStatus, errorThrown) {
        $("#statusTrainer").text("Please wait as trainer has not started yet. Error: " + textStatus + "  " + errorThrown);
    });
}, 10 * 1000);