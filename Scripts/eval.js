$(function () {
    windowType = '5m';

    function updateDataD3(baseEvalAddress, chartId) {
        d3.json(baseEvalAddress + '?windowType=' + windowType, function (error, response) {
            nv.addGraph(function () {
                var chart = nv.models.cumulativeLineChart()
                              .x(function (d) { return parseInt(d[0].substr(6)) })
                              .y(function (d) { return d[1] })
                              .color(d3.scale.category10().range())
                              .useInteractiveGuideline(true);

                chart.xAxis
                   .tickFormat(function (d) {
                       return d3.time.format('%X')(new Date(d))
                   });
                chart.yAxis
                    .tickFormat(d3.format(',.1'))
                    .showMaxMin(false);

                if (response == null) {
                    response = [];
                }
                d3.select('#' + chartId + ' svg')
                    .datum(response)
                    .call(chart);

                nv.utils.windowResize(chart.update);

                return chart;
            });
        });
    }

    function updateData() {
        updateDataD3('/Home/EvalJson', 'chart');
    }
    function updateDataAPI() { // update chart for API demo
        updateDataD3('/Home/EvalJsonAPI', 'chart-api');
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
    }, 1000 * 60);

    $('#eval-window-filter').on('change', function () {
        windowType = this.value;
        updateData();
    });
})