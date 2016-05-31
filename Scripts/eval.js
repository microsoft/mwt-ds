$(function () {
    windowType = '3h';

    updateData();

    var inter = setInterval(function () {
        updateData();
    }, 1000 * 60);

    function updateData() {
        d3.json('/Home/EvalJson?windowType=' + windowType, function (error, response) {
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

                d3.select('#chart svg')
                    .datum(response)
                    .call(chart);

                nv.utils.windowResize(chart.update);

                return chart;
            });
        });
    }

    $('#eval-window-filter').on('change', function () {
        windowType = this.value;
        updateData();
    });
})