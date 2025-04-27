lastkeys = nil
server = nil
ST_sockets = {}
nextID = 1

function ST_stop(id)
	local sock = ST_sockets[id]
	ST_sockets[id] = nil
	sock:close()
end

function ST_format(id, msg, isError)
	local prefix = "Socket " .. id
	if isError then
		prefix = prefix .. " Error: "
	else
		prefix = prefix .. " Received: "
	end
	return prefix .. msg
end

function ST_error(id, err)
	console:error(ST_format(id, err, true))
	ST_stop(id)
end

function ST_received(id)
	console:log("ST_received 1")
	local sock = ST_sockets[id]
	if not sock then return end
	while true do
		local message, err = sock:receive(1024)
		console:log("ST_received 2")
		if message then
			console:log("ST_received 3")
			console:log(ST_format(id, message:match("^(.-)%s*$")))
			sock:send(message)
		else			
			console:log("ST_received 4")
			if err ~= socket.ERRORS.AGAIN then
				console:log("ST_received 5")
				if err == "disconnected" then
					console:log("ST_received 6")
					console:log(ST_format(id, err, true))
				elseif err == socket.ERRORS.UNKNOWN_ERROR then
					console:log("ST_received 7")
					console:log(ST_format(id, err, true))
				else
					console:log("ST_received 8")
					console:error(ST_format(id, err, true))
				end
				ST_stop(id)
			end
			return
		end
	end
end

function ST_accept()
	local sock, err = server:accept()
	if err then
		console:error(ST_format("Accept", err, true))
		return
	end
	local id = nextID
	nextID = id + 1
	ST_sockets[id] = sock
	sock:add("received", function() ST_received(id) end)
	sock:add("error", function() ST_error(id) end)
	console:log(ST_format(id, "Connected"))
end

local port = 8888
server = nil
while not server do
	server, err = socket.bind(nil, port)
	if err then
		if err == socket.ERRORS.ADDRESS_IN_USE then
			port = port + 1
		else
			console:error(ST_format("Bind", err, true))
			break
		end
	else
		local ok
		ok, err = server:listen()
		if err then
			server:close()
			console:error(ST_format("Listen", err, true))
		else
			console:log("Socket Server Test: Listening on port " .. port)
			server:add("received", ST_accept)
		end
	end
end