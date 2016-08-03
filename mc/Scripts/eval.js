$(function () {
    $.ajaxSetup({ cache: false });

    windowType = '6d';
    getDataFromCache = false;

    function updateDataD3(baseEvalAddress, chartId) {
        d3.selectAll(".nvtooltip").remove();

        var noCacheParameter = Math.floor(Math.random() * 1000); // additional measure to prevent caching of JSON result in all browsers
        d3.json(baseEvalAddress + 'windowType=' + windowType + '&useCache=' + getDataFromCache + '&' + noCacheParameter, function (error, response) {
            nv.addGraph(function () {
                var chart = nv.models.lineWithFocusChart()
                              .x(function (d) { return parseInt(d[0].substr(6)) })
                              .y(function (d) { return d[1] })
                              .color(d3.scale.category10().range())
                              .useInteractiveGuideline(true);

                chart.xAxis
                   .tickFormat(function (d) {
                       return d3.time.format('%X')(new Date(d))
                   });
                chart.x2Axis
                   .tickFormat(function (d) {
                       return d3.time.format('%X')(new Date(d))
                   });
                chart.yAxis
                    .tickFormat(d3.format(',.2f'))
                    .showMaxMin(false);
                chart.y2Axis
                    .tickFormat(d3.format(',.2f'))
                    .showMaxMin(false);

                chart.xAxis.axisLabel("Time");
                chart.yAxis.axisLabel("Average Reward");

                var updateGraph = true;
                if (response == null) {
                    response = { Data: [] };
                }
                else {
                    if (response.DataError) {
                        if (response.DataError == 304) {
                            updateGraph = false;
                            $('#eval-chart-status').text("No new data detected.");
                        }
                        else {
                            $('#eval-chart-status').text(response.DataError);
                            response.Data = [];
                        }
                    }
                    $("#statusTrainer").text(response.TrainerStatus + " (Last updated at: " + moment().format('MMMM Do YYYY, h:mm:ss a') + ")");
                    if ($('#statusModel').length) {
                        // TODO: refactor
                        modelTime = new Date(parseInt(response.ModelUpdateTime.substr(6)));
                        modelTimeMessage = 'Latest model obtained at: ' + moment(modelTime).format('MMMM Do YYYY, h:mm:ss a');
                        if (modelTime.getFullYear() <= 1) {
                            modelTimeMessage = ''
                        }
                        $("#statusModel").text(modelTimeMessage);
                    }
                }
                if (updateGraph) {
                    d3.select('#' + chartId + ' svg')
                        .datum(response.Data)
                        .call(chart);

                    nv.utils.windowResize(chart.update);

                    $('#eval-chart-status').text('Graph updated at: ' + moment().format('MMMM Do YYYY, h:mm:ss a'));
                }

                return chart;
            });
        });
        getDataFromCache = true;
    }

    function updateData() {
        updateDataD3('/Home/EvalJson?', 'chart');
    }
    function updateDataAPI() { // update chart for API demo
        updateDataD3('/Home/EvalJsonAPI?userToken=' + $("#userToken").val() + '&', 'chart-api');
    }
    function updateChart() {
        if ($("#chart").length) {
            updateData();
        }
        if ($("#chart-api").length) {
            updateDataAPI();
        }
    }
    updateChart();

    var inter = setInterval(function () {
        updateChart();
    }, 1000 * 15);

    $('#eval-window-filter').on('change', function () {
        windowType = this.value;
        if ($('#window-size').length) {
            $('#window-size').text(windowType);
        }
        updateChart();
    });
})