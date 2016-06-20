$(function () {
    windowType = '1m';

    function updateDataD3(baseEvalAddress, chartId) {
        width = 400;
        height = 400;
        // Adds the svg canvas
        var svg = d3.select("body")
            .append("svg")
                .attr("width", width)
                .attr("height", height)
            .append("g")
                .attr("transform",
                      "translate(" + 0 + "," + 0 + ")");

        // Set the ranges
        var x = d3.time.scale().range([0, width]);
        var y = d3.scale.linear().range([height, 0]);

        // Define the line
        var valueline = d3.svg.line()
            .x(function (d) { return x(parseInt(d[0].substr(6))); })
            .y(function (d) { return y(d[1]); });

        d3.json(baseEvalAddress + 'windowType=' + windowType, function (error, response) {
            if (response == null) {
                response = [];
            }
            // Add the valueline path.
            svg.append("path")
                .attr("class", "line")
                .attr("d", valueline(response));

            //d3.select('#' + chartId + ' svg')
            //    .datum(response)
            //    .call(chart);

            //nv.addGraph(function () {
            //    //var chart = nv.models.lineChart()
            //    //              .x(function (d) { return parseInt(d[0].substr(6)) })
            //    //              .y(function (d) { return d[1] })
            //    //              .color(d3.scale.category10().range())
            //    //              .useInteractiveGuideline(true);

            //    //chart.xAxis
            //    //   .tickFormat(function (d) {
            //    //       return d3.time.format('%X')(new Date(d))
            //    //   });
            //    //chart.yAxis
            //    //    .tickFormat(d3.format(',.1'))
            //    //    .showMaxMin(false);

            //    d3.select('#' + chartId + ' svg')
            //        .datum(response)
            //        .call(chart);

            //    //nv.utils.windowResize(chart.update);

            //    return chart;
            //});
        });
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
        updateData();
    });
})