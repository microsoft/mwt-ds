var assert = require('assert');

var DS = require('../decision-service');

describe('Decision Service', function() {

    describe('#contextToDSPath()', function() {

        it('should return DS formated path', function() {

            var json = {
                "a015af": {
                    "_multi": [{
                        "_id": "251878",
                        "trending": true
                    }, {
                        "_id": "251896"
                    }]
                }
            }

            var path = DS.contextToDSPath(json);

            assert.equal('~a015af/251878;trending/251896', path);

        });

        it('should return DS formated path', function() {

            var json = {
                "a123": {
                    "_multi": [{
                        "_id": "1"
                    }, {
                        "_id": "2"
                    }]
                },
                "b234": {
                    "_id": "4",
                    "y": // 
                    {
                        "d": true,
                        "f": true,
                        "g": true
                    },
                    "_multi": [{
                        "_id": "def",
                        "x": {
                            "tr": 2.3,
                            "bg": true,
                            "ui": 2.1,
                        },
                        "a": .2
                    }]
                }
            }

            var path = DS.contextToDSPath(json);

            assert.equal('~a123/1/2/~b234!4;y~d,f,g/def;x~tr=2.3,bg,ui=2.1;a=0.2', path);
        });
    });

    it('should return DS formated path', function() {
        //http://ds.microsoft.com/api/rank/dscb/~a015af/251878;trending/251896/~2c61bd/123/456/789?detail=1
        //
        //
        var options = {
            endpoint: 'rank',
            jsonpCallbackName: 'dscb',
            detail: true,
            context: {
                "a015af": {
                    "_multi": [{
                        "_id": "251878",
                        "trending": true
                    }, {
                        "_id": "251896"
                    }]
                },
                "2c61bd": {
                    "_multi": [{
                        "_id": "123"
                    }, {
                        "_id": "456"
                    }, {
                        "_id": "789"
                    }]
                }
            }
        };

        var url = DS.generateScriptUrl(options);

        assert.equal('http://ds.microsoft.com/api/rank/dscb/~a015af/251878;trending/251896/~2c61bd/123/456/789?detail=1', url);
    });
});