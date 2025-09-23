# Vooshi-TTS
TODO

# For Developer

Untuk mulai develop:
1. Sebelum mulai, pastiin udah build atau patch MateEngine jadi unstripped [Unstripping assemblies](#head1234)
2. Setup melon mod dulu https://melonwiki.xyz/#/
3. Pastiin patch game, launch sekali, lalu pastiin banner splash dari melon udah keliatan di consolenya
4. Clone repo ini, lalu mulai setting beberapa config yang perlu diubah
    1. `vooshi-config.txt` | Untuk basic connection
    2. `Vooshi-TTS.csproj` | Di line 19 ada variable `MateEnginePath` ganti dengan path MateEngine yang udah unstripped dan udah dipatch pake Melon
5. Project ini depends on RabbitMQ, so resolve juga itu pakai NuGet!
6. You are ready to go!

## <a name="unstripping"></a>Unstripping assemblies
TODO