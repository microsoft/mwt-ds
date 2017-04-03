var webpack = require("webpack");

module.exports = {
    entry: './src/DecisionService.ts',
    output: {
        filename: './build/DecisionService.js'
    },
    resolve: {
        extensions: ['.Webpack.js', '.web.js', '.ts', '.js', '.tsx']
    },
    module: {
        loaders: [
            {
                test: /\.ts(x?)$/,
                exclude: ['./node_modules/'],
                loader: 'ts-loader'
            }
        ]
    },
    plugins: [
        new webpack.optimize.UglifyJsPlugin({ minimize: true })
    ]
}